using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Configurations;
using BusinessAnalytics.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusinessAnalytics.Services.Realization
{
    public class TelegramReportService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TelegramSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TelegramReportService> _logger;

        private long _lastUpdateId = 0; // Tracks processed Telegram updates

        public TelegramReportService(
            IServiceScopeFactory scopeFactory,
            IOptions<TelegramSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<TelegramReportService> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_settings.BotToken) || _settings.BotToken == "YOUR_BOT_TOKEN_HERE")
            {
                _logger.LogWarning("⚠️ TelegramSettings.BotToken не налаштовано. Сервіс не запущено.");
                return;
            }

            _logger.LogInformation("🤖 Telegram Service запущено. Звіт о {Hour}:00 щодня.", _settings.ReportHour);

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"https://api.telegram.org/bot{_settings.BotToken}/deleteWebhook", stoppingToken);
                _logger.LogInformation("🧹 Webhook status: {Status}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Не вдалося видалити webhook на старті.");
            }

            var pollingTask = RunPollingLoopAsync(stoppingToken);
            var reportTask = RunDailyReportLoopAsync(stoppingToken);
            var reminderTask = RunWeeklyUnpaidExpensesLoopAsync(stoppingToken);

            await Task.WhenAll(pollingTask, reportTask, reminderTask);
        }


        private async Task RunPollingLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollAndHandleUpdatesAsync(ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Помилка під час polling.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), ct).ContinueWith(_ => { }); // Suppress cancellation exception
            }
        }

        private async Task PollAndHandleUpdatesAsync(CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{_settings.BotToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=25&allowed_updates=[\"message\"]";

            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("⚠️ Помилка getUpdates (HTTP {Status}): {Body}", response.StatusCode, errorBody);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<TelegramUpdateResponse>(json);

            if (result?.Ok != true || result.Result == null)
            {
                _logger.LogWarning("⚠️ Некоректна відповідь Telegram API: {Json}", json);
                return;
            }

            foreach (var update in result.Result)
            {
                _lastUpdateId = update.UpdateId;

                var text = update.Message?.Text?.Trim();
                var chatId = update.Message?.Chat?.Id.ToString();

                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(chatId)) continue;

                if (text.StartsWith("/start"))
                {
                    var parts = text.Split(' ', 2);
                    var token = parts.Length > 1 ? parts[1].Trim() : null;

                    if (!string.IsNullOrEmpty(token))
                    {
                        await HandleLinkTokenAsync(token, chatId, ct);
                    }
                    else
                    {
                        await SendMessageAsync(chatId,
                            "👋 Вітаю! Щоб прив'язати ваш акаунт Business Analytics, перейдіть у *Профіль → Telegram звіти* та натисніть кнопку «Прив'язати Telegram».",
                            ct);
                    }
                }
            }
        }

        private async Task HandleLinkTokenAsync(string token, string chatId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(
                u => u.TelegramLinkToken == token && u.TelegramLinkTokenExpiry > DateTime.UtcNow, ct);

            if (user == null)
            {
                await SendMessageAsync(chatId,
                    "❌ Посилання недійсне або застаріло. Згенеруйте нове в профілі Business Analytics.",
                    ct);
                return;
            }

            user.TelegramChatId = chatId;
            user.TelegramLinkToken = null;
            user.TelegramLinkTokenExpiry = null;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("✅ Telegram прив'язано для {Email}, chatId={ChatId}", user.Email, chatId);

            await SendMessageAsync(chatId,
                $"✅ *Telegram успішно прив'язано!*\n\n" +
                $"Щодня о *{_settings.ReportHour}:00* ви будете отримувати фінансовий звіт по ваших бізнесах.\n\n" +
                $"📊 _Business Analytics_",
                ct);
        }


        private async Task RunDailyReportLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = DateTime.Today.AddHours(_settings.ReportHour);
                if (now >= nextRun) nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("⏰ Наступний звіт: {NextRun}", nextRun);

                try { await Task.Delay(delay, ct); }
                catch (TaskCanceledException) { break; }

                await SendDailyReportsToAllUsersAsync(ct);
            }
        }

        private async Task SendDailyReportsToAllUsersAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var proUserRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "ProUser", ct);
                if (proUserRole == null) return;
                var proUserIds = await db.UserRoles.Where(ur => ur.RoleId == proUserRole.Id).Select(ur => ur.UserId).ToListAsync(ct);

                var users = await db.Users
                    .Where(u => u.TelegramChatId != null && u.TelegramChatId != "" && proUserIds.Contains(u.Id))
                    .ToListAsync(ct);

                if (!users.Any()) return;

                int sent = 0;
                foreach (var user in users)
                {
                    var businesses = await db.BusinessAccounts
                        .Include(b => b.Transactions.Where(t => t.Date >= today && t.Date < tomorrow))
                        .Where(b => b.OwnerId == user.Id)
                        .ToListAsync(ct);

                    if (!businesses.Any()) continue;

                    var message = BuildReportMessage(businesses, today, user.Email ?? "");
                    await SendMessageAsync(user.TelegramChatId!, message, ct);
                    sent++;
                }

                _logger.LogInformation("✅ Щоденний звіт надіслано {Count} користувачам.", sent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Помилка при надсиланні звітів.");
            }
        }


        private async Task RunWeeklyUnpaidExpensesLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = DateTime.Today.AddHours(9);
                while (nextRun.DayOfWeek != DayOfWeek.Monday || now >= nextRun)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - now;
                _logger.LogInformation("Наступне нагадування про витрати: {NextRun}", nextRun);

                try { await Task.Delay(delay, ct); }
                catch (TaskCanceledException) { break; }

                await SendUnpaidExpensesRemindersAsync(ct);
            }
        }

        private async Task SendUnpaidExpensesRemindersAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var proUserRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "ProUser", ct);
                if (proUserRole == null) return;
                var proUserIds = await db.UserRoles.Where(ur => ur.RoleId == proUserRole.Id).Select(ur => ur.UserId).ToListAsync(ct);

                var users = await db.Users
                    .Where(u => u.TelegramChatId != null && u.TelegramChatId != "" && proUserIds.Contains(u.Id))
                    .ToListAsync(ct);

                if (!users.Any()) return;

                int sent = 0;
                foreach (var user in users)
                {
                    var unpaidRecords = await db.UtilityRecords
                        .Include(r => r.Premises)
                            .ThenInclude(p => p.BusinessAccount)
                        .Include(r => r.UtilityType)
                        .Where(r => r.Premises.BusinessAccount.OwnerId == user.Id && !r.IsPaid)
                        .OrderBy(r => r.Premises.BusinessAccount.Name)
                        .ThenBy(r => r.Year).ThenBy(r => r.Month)
                        .ToListAsync(ct);

                    if (!unpaidRecords.Any()) continue;

                    var message = BuildUnpaidExpensesMessage(unpaidRecords, user.Email ?? "");
                    await SendMessageAsync(user.TelegramChatId!, message, ct);
                    sent++;
                }

                _logger.LogInformation("Нагадування про неоплачені витрати надіслано {Count} користувачам.", sent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при надсиланні нагадувань.");
            }
        }

        private string BuildUnpaidExpensesMessage(List<UtilityRecord> records, string userEmail)
        {
            var sb = new StringBuilder();
            sb.AppendLine("*Нагадування: Неоплачені операційні витрати*");
            if (!string.IsNullOrEmpty(userEmail))
                sb.AppendLine($"Користувач: {EscapeMarkdown(userEmail)}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");

            var groupedByBiz = records.GroupBy(r => r.Premises.BusinessAccount.Name);
            var monthNames = new[] { "", "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень", "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень" };

            foreach (var group in groupedByBiz)
            {
                sb.AppendLine();
                sb.AppendLine($"*{EscapeMarkdown(group.Key)}*");
                
                foreach (var r in group)
                {
                    sb.AppendLine($"- {EscapeMarkdown(r.Premises.Name)} — {EscapeMarkdown(r.UtilityType.Name)}");
                    sb.AppendLine($"  Період: {monthNames[r.Month]} {r.Year} | Сума: `{r.Amount:N2} ₴`");
                }
            }

            var totalDebt = records.Sum(r => r.Amount);
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"*Загальна сума до оплати:* `{totalDebt:N2} ₴`");
            sb.AppendLine();
            sb.AppendLine($"_Business Analytics_");
            
            return sb.ToString();
        }


        private string BuildReportMessage(List<BusinessAccount> businesses, DateTime date, string userEmail = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📊 *Щоденний звіт — {date:dd.MM.yyyy}*");
            if (!string.IsNullOrEmpty(userEmail))
                sb.AppendLine($"👤 {EscapeMarkdown(userEmail)}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");

            bool hasAny = false;
            foreach (var biz in businesses)
            {
                var txList = biz.Transactions.ToList();
                var income = txList.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount);
                var expense = txList.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount);
                var profit = income - expense;

                sb.AppendLine();
                sb.AppendLine($"🏢 *{EscapeMarkdown(biz.Name)}*");

                if (!txList.Any())
                {
                    sb.AppendLine("   _Транзакцій сьогодні немає_");
                }
                else
                {
                    hasAny = true;
                    sb.AppendLine($"   💰 Дохід:    `{income:N2} ₴`");
                    sb.AppendLine($"   💸 Витрати:  `{expense:N2} ₴`");
                    sb.AppendLine($"   📈 Прибуток: `{profit:N2} ₴`");
                    sb.AppendLine($"   🔢 Транзакцій: *{txList.Count}*");
                }
            }

            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");

            if (hasAny)
            {
                var totalIn = businesses.Sum(b => b.Transactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount));
                var totalEx = businesses.Sum(b => b.Transactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount));
                sb.AppendLine($"📦 *Загальний підсумок:*");
                sb.AppendLine($"   💰 Дохід:    `{totalIn:N2} ₴`");
                sb.AppendLine($"   💸 Витрати:  `{totalEx:N2} ₴`");
                sb.AppendLine($"   📈 Прибуток: `{totalIn - totalEx:N2} ₴`");
            }
            else
            {
                sb.AppendLine("_Сьогодні жодних транзакцій не зафіксовано._");
            }

            sb.AppendLine();
            sb.AppendLine($"_Business Analytics · {DateTime.Now:HH:mm}_");
            return sb.ToString();
        }

        private static string EscapeMarkdown(string text) =>
            text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`");


        private async Task SendMessageAsync(string chatId, string text, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{_settings.BotToken}/sendMessage";
            var payload = new { chat_id = chatId, text, parse_mode = "Markdown" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogError("Telegram sendMessage error: {Code}", response.StatusCode);
        }


        private class TelegramUpdateResponse
        {
            [JsonPropertyName("ok")] public bool Ok { get; set; }
            [JsonPropertyName("result")] public List<TelegramUpdate>? Result { get; set; }
        }

        private class TelegramUpdate
        {
            [JsonPropertyName("update_id")] public long UpdateId { get; set; }
            [JsonPropertyName("message")] public TelegramMessage? Message { get; set; }
        }

        private class TelegramMessage
        {
            [JsonPropertyName("text")] public string? Text { get; set; }
            [JsonPropertyName("chat")] public TelegramChat? Chat { get; set; }
        }

        private class TelegramChat
        {
            [JsonPropertyName("id")] public long Id { get; set; }
        }
    }
}
