using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Models.ViewModels;
using BusinessAnalytics.Models.ViewModels.BusinessAnalytics.Models.ViewModels;
using BusinessAnalytics.Models.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BusinessAnalytics.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly TelegramSettings _telegramSettings;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            IOptions<TelegramSettings> telegramSettings)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _telegramSettings = telegramSettings.Value;
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("User"))
                    await _roleManager.CreateAsync(new IdentityRole("User"));

                await _userManager.AddToRoleAsync(user, "User");

                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                
                string subject = "Підтвердження реєстрації в Business Analytics";
                string message = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #0f172a; color: #f8fafc; padding: 30px; border-radius: 10px; border: 1px solid #00ff88;'>
                        <h2 style='color: #00ff88; text-align: center;'>Вітаємо у Business Analytics!</h2>
                        <p style='font-size: 16px;'>Ваш код для підтвердження реєстрації:</p>
                        <div style='background-color: rgba(0, 255, 136, 0.1); padding: 15px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                            <strong style='font-size: 24px; color: #00ff88; letter-spacing: 5px;'>{code}</strong>
                        </div>
                        <p style='font-size: 14px; color: #94a3b8;'>Нікому не повідомляйте цей код.</p>
                    </div>";

                await _emailSender.SendEmailAsync(user.Email, subject, message);

                return RedirectToAction("VerifyEmail", new { email = user.Email });
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");
            return View(new VerifyEmailViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return RedirectToAction("Login");

            var result = await _userManager.ConfirmEmailAsync(user, model.Code);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Невірний код підтвердження.");
            return View(model);
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError("", "Будь ласка, підтвердіть вашу електронну пошту.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Невірний логін або пароль");
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return RedirectToAction("ForgotPasswordConfirmation");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Account", new { email = model.Email, token = token }, protocol: Request.Scheme);

            string subject = "Відновлення пароля";
            string message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #0f172a; color: #f8fafc; padding: 30px; border-radius: 10px; border: 1px solid #00ff88;'>
                    <h2 style='color: #00ff88; text-align: center;'>Відновлення пароля</h2>
                    <p style='font-size: 16px;'>Для скидання пароля перейдіть за посиланням нижче:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{callbackUrl}' style='background-color: #00ff88; color: #020617; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Скинути пароль</a>
                    </div>
                </div>";

            await _emailSender.SendEmailAsync(model.Email, subject, message);

            return RedirectToAction("ForgotPasswordConfirmation");
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? token, string? email)
        {
            if (token == null || email == null)
            {
                return RedirectToAction("Login");
            }
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        // GET: /Account/Profile
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var model = new ProfileViewModel
            {
                Email = user.Email ?? "",
                UserName = user.UserName,
                EmailConfirmed = user.EmailConfirmed,
                TelegramChatId = user.TelegramChatId
            };

            ViewBag.BotUsername = _telegramSettings.BotUsername;
            ViewBag.ReportHour = _telegramSettings.ReportHour;
            var linkToken = TempData["TelegramLinkToken"] as string;
            if (!string.IsNullOrEmpty(linkToken))
                ViewBag.TelegramDeepLink = $"https://t.me/{_telegramSettings.BotUsername}?start={linkToken}";

            return View(model);
        }

        // POST: /Account/GenerateTelegramLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GenerateTelegramLink()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            user.TelegramLinkToken = Guid.NewGuid().ToString("N"); // e.g. "a1b2c3d4..."
            user.TelegramLinkTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await _userManager.UpdateAsync(user);

            TempData["TelegramLinkToken"] = user.TelegramLinkToken;
            TempData["ProfileTab"] = "telegram";
            return RedirectToAction("Profile");
        }

        // POST: /Account/SaveTelegramChatId
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> SaveTelegramChatId(string telegramChatId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            user.TelegramChatId = string.IsNullOrWhiteSpace(telegramChatId) ? null : telegramChatId.Trim();
            await _userManager.UpdateAsync(user);

            TempData["ProfileSuccess"] = string.IsNullOrWhiteSpace(telegramChatId)
                ? "Телеграм відключено."
                : "Телеграм успішно прив'язано! ";
            return RedirectToAction("Profile");
        }

        // POST: /Account/SendTestTelegramReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> SendTestTelegramReport()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.TelegramChatId))
            {
                TempData["ProfileError"] = "Спочатку прив'яжіть Telegram!";
                TempData["ProfileTab"] = "telegram";
                return RedirectToAction("Profile");
            }

            var db = (BusinessAnalytics.Data.ApplicationDbContext)HttpContext.RequestServices.GetService(typeof(BusinessAnalytics.Data.ApplicationDbContext))!;
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Fetch businesses with today's transactions
            var businesses = db.BusinessAccounts
                .Where(b => b.OwnerId == user.Id)
                .Select(b => new {
                    b.Name,
                    Transactions = db.Transactions.Where(t => t.BusinessAccountId == b.Id && t.Date >= today && t.Date < tomorrow).ToList()
                })
                .ToList();

            // Build report text
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🚀 *Тестовий звіт — {today:dd.MM.yyyy}*");
            sb.AppendLine($"👤 {user.Email}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");

            bool hasAny = false;
            foreach (var biz in businesses)
            {
                var txList = biz.Transactions;
                var income = txList.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount);
                var expense = txList.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount);
                var profit = income - expense;

                sb.AppendLine();
                sb.AppendLine($"🏢 *{biz.Name}*");

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
                    sb.AppendLine($"   🔢 : *{txList.Count}*");
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

            try
            {
                using var client = new HttpClient();
                var url = $"https://api.telegram.org/bot{_telegramSettings.BotToken}/sendMessage";
                var payload = new { chat_id = user.TelegramChatId, text = sb.ToString(), parse_mode = "Markdown" };
                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    TempData["ProfileSuccess"] = "🚀 Тестовий звіт успішно надіслано в Telegram!";
                }
                else
                {
                    var err = await response.Content.ReadAsStringAsync();
                    TempData["ProfileError"] = $"Помилка відправки в Telegram: {response.StatusCode} - {err}";
                }
            }
            catch (Exception ex)
            {
                TempData["ProfileError"] = $"Помилка відправки: {ex.Message}";
            }

            TempData["ProfileTab"] = "telegram";
            return RedirectToAction("Profile");
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            if (!ModelState.IsValid)
            {
                TempData["ProfileTab"] = "password";
                TempData["PasswordError"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction("Profile");
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["ProfileSuccess"] = "Пароль успішно змінено!";
                return RedirectToAction("Profile");
            }

            TempData["ProfileTab"] = "password";
            TempData["PasswordError"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Profile");
        }

        // POST: /Account/RequestEmailChange
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> RequestEmailChange(ChangeEmailViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            if (!ModelState.IsValid)
            {
                TempData["ProfileTab"] = "email";
                TempData["EmailError"] = "Невірний формат email.";
                return RedirectToAction("Profile");
            }

            var existing = await _userManager.FindByEmailAsync(model.NewEmail);
            if (existing != null)
            {
                TempData["ProfileTab"] = "email";
                TempData["EmailError"] = "Ця пошта вже використовується іншим акаунтом.";
                return RedirectToAction("Profile");
            }

            var code = new Random().Next(100000, 999999).ToString();

            TempData["EmailChangeCode"] = code;
            TempData["EmailChangeTarget"] = model.NewEmail;

            string subject = "Підтвердження зміни пошти — Business Analytics";
            string htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;
                            background-color: #0f172a; color: #f8fafc; padding: 30px;
                            border-radius: 10px; border: 1px solid #00ff88;'>
                    <h2 style='color: #00ff88; text-align: center;'>Зміна електронної пошти</h2>
                    <p style='font-size: 16px;'>Ви запросили зміну email на <strong>{model.NewEmail}</strong>.</p>
                    <p>Ваш код підтвердження:</p>
                    <div style='background-color: rgba(0,255,136,0.1); padding: 15px; text-align: center;
                                border-radius: 5px; margin: 20px 0;'>
                        <strong style='font-size: 28px; color: #00ff88; letter-spacing: 8px;'>{code}</strong>
                    </div>
                    <p style='font-size: 13px; color: #94a3b8;'>Якщо ви не робили цей запит — проігноруйте лист.</p>
                </div>";

            await _emailSender.SendEmailAsync(model.NewEmail, subject, htmlMessage);

            return RedirectToAction("ConfirmEmailChange");
        }

        // GET: /Account/ConfirmEmailChange
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult ConfirmEmailChange()
        {
            var target = TempData.Peek("EmailChangeTarget") as string;
            if (string.IsNullOrEmpty(target)) return RedirectToAction("Profile");
            TempData.Keep("EmailChangeCode");
            TempData.Keep("EmailChangeTarget");
            return View(new ConfirmEmailChangeViewModel { NewEmail = target });
        }

        // POST: /Account/ConfirmEmailChange
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeViewModel model)
        {
            var savedCode = TempData["EmailChangeCode"] as string;
            var savedEmail = TempData["EmailChangeTarget"] as string;

            if (savedCode == null || savedEmail == null)
            {
                TempData["ProfileError"] = "Сесія підтвердження закінчилась. Спробуйте ще раз.";
                return RedirectToAction("Profile");
            }

            if (model.Code != savedCode)
            {
                TempData["EmailChangeCode"] = savedCode;
                TempData["EmailChangeTarget"] = savedEmail;
                ModelState.AddModelError("", "Невірний код підтвердження.");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var token = await _userManager.GenerateChangeEmailTokenAsync(user, savedEmail);
            var result = await _userManager.ChangeEmailAsync(user, savedEmail, token);
            if (result.Succeeded)
            {
                user.UserName = savedEmail;
                await _userManager.UpdateAsync(user);
                await _signInManager.RefreshSignInAsync(user);
                TempData["ProfileSuccess"] = "Пошту успішно змінено! 📧";
                return RedirectToAction("Profile");
            }

            ModelState.AddModelError("", string.Join("; ", result.Errors.Select(e => e.Description)));
            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
