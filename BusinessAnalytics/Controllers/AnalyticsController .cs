using Microsoft.EntityFrameworkCore;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace BusinessAnalytics.Controllers
{
    [Authorize]
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBusinessService _businessService;
        private readonly BusinessAnalytics.Data.ApplicationDbContext _context;

        public AnalyticsController(
            IAnalyticsService analyticsService,
            UserManager<ApplicationUser> userManager,
            IBusinessService businessService,
            BusinessAnalytics.Data.ApplicationDbContext context)
        {
            _analyticsService = analyticsService;
            _userManager = userManager;
            _businessService = businessService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var overviews = await _analyticsService.GetUserBusinessOverviewsAsync(userId);
            return View(overviews);
        }

        [HttpGet]
        public async Task<IActionResult> Compare()
        {
            var userId = _userManager.GetUserId(User);
            var businesses = await _businessService.GetBusinessesByUserAsync(userId);
            ViewBag.Businesses = businesses;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Compare(List<Guid> businessIds, DateTime startDate, DateTime endDate)
        {
            if (businessIds == null || !businessIds.Any())
            {
                ModelState.AddModelError("", "Будь ласка, виберіть хоча б один бізнес для порівняння.");
                return View();
            }

            var result = await _analyticsService.CompareBusinessesAsync(businessIds, startDate, endDate);
            return View("CompareResult", result);
        }
        [HttpPost]
        public async Task<IActionResult> GetAnalyticsData([FromBody] AnalyticsRequestDTO request)
        {
            if (request.BusinessIds == null || !request.BusinessIds.Any())
                return BadRequest("Не вибрано жодного бізнесу.");

            var result = await _analyticsService.CompareBusinessesAsync(
                request.BusinessIds,
                request.StartDate,
                request.EndDate
            );

            foreach (var business in result.Businesses)
            {
                business.Metrics = business.Metrics
                    .Where(m => request.SelectedMetrics.Contains(m.Key))
                    .ToDictionary(m => m.Key, m => m.Value);
            }

            return Json(result);
        }
        [HttpPost]
        [Route("Analytics/GeneratePdf")]
        public async Task<IActionResult> GeneratePdf([FromBody] GeneratePdfRequest request)
        {
            if (request == null || !request.BusinessIds.Any())
                return BadRequest("Invalid request.");

            var businesses = await _context.BusinessAccounts
                .Where(b => request.BusinessIds.Contains(b.Id))
                .ToListAsync();

            var startDate = request.StartDate.Date;
            var endDate = request.EndDate.Date.AddDays(1).AddTicks(-1);
            var days = (endDate.Date - startDate).Days + 1;
            var prevEndDate = startDate.AddDays(-1).Date.AddDays(1).AddTicks(-1);
            var prevStartDate = prevEndDate.Date.AddDays(-days + 1);

            var currentTx = await _context.Transactions
                .Where(t => request.BusinessIds.Contains(t.BusinessAccountId) && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            var prevTx = await _context.Transactions
                .Where(t => request.BusinessIds.Contains(t.BusinessAccountId) && t.Date >= prevStartDate && t.Date <= prevEndDate)
                .ToListAsync();

            var currentIncome = currentTx.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount);
            var currentExpenses = currentTx.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount);
            var currentProfit = currentIncome - currentExpenses;

            var prevIncome = prevTx.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount);
            var prevExpenses = prevTx.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount);
            var prevProfit = prevIncome - prevExpenses;

            var monthsInPeriod = new System.Collections.Generic.List<(int Year, int Month)>();
            for(var d = new DateTime(startDate.Year, startDate.Month, 1); d <= endDate; d = d.AddMonths(1))
            {
                monthsInPeriod.Add((d.Year, d.Month));
            }

            var utilitiesQuery = _context.UtilityRecords
                .Include(u => u.Premises)
                .Include(u => u.UtilityType)
                .Where(u => request.BusinessIds.Contains(u.Premises.BusinessAccountId))
                .AsEnumerable()
                .Where(u => monthsInPeriod.Contains((u.Year, u.Month)))
                .ToList();

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Фінансовий звіт").SemiBold().FontSize(24).FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Період: {startDate:dd.MM.yyyy} — {endDate:dd.MM.yyyy}").FontSize(14).FontColor(Colors.Grey.Medium);
                        col.Item().Text($"Бізнеси: {string.Join(", ", businesses.Select(b => b.Name))}").FontSize(12);
                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(20).Row(row =>
                        {
                            row.RelativeItem().Column(c => {
                                c.Item().Text("ОБОРОТ").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{currentIncome:N2} ₴").FontSize(18).SemiBold().FontColor(Colors.Green.Darken2);
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().Text("ВИТРАТИ").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{currentExpenses:N2} ₴").FontSize(18).SemiBold().FontColor(Colors.Red.Darken2);
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().Text("ЧИСТИЙ ПРИБУТОК").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{currentProfit:N2} ₴").FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                            });
                        });

                        col.Item().PaddingBottom(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);

                        col.Item().PaddingBottom(10).Text("Аналітика операційних витрат").FontSize(16).SemiBold();
                        
                        if (utilitiesQuery.Any())
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Об'єкт / Філія").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Стаття").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Сума").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Статус").SemiBold();
                                });

                                foreach (var u in utilitiesQuery.OrderBy(u => u.Premises.Name).ThenBy(u => u.Year).ThenBy(u => u.Month))
                                {
                                    table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Text(u.Premises.Name);
                                    table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Text($"{u.UtilityType.Name} ({u.Month:00}.{u.Year})");
                                    table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Text($"{u.Amount:N2} ₴");
                                    
                                    var statusText = u.IsPaid ? "Оплачено" : "Неоплачено";
                                    var statusColor = u.IsPaid ? Colors.Green.Medium : Colors.Red.Medium;
                                    table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Text(statusText).FontColor(statusColor).SemiBold();
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text("Немає записів про операційні витрати за вибраний період.").FontColor(Colors.Grey.Medium).Italic();
                        }

                        col.Item().PaddingVertical(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);

                        col.Item().PaddingBottom(10).Text("Динаміка (порівняно з попереднім періодом)").FontSize(16).SemiBold();
                        col.Item().PaddingBottom(5).Text($"Попередній період: {prevStartDate:dd.MM.yyyy} — {prevEndDate:dd.MM.yyyy}").FontSize(10).FontColor(Colors.Grey.Medium);

                        Func<decimal, decimal, string> getDiffStr = (curr, prev) => 
                        {
                            if (prev == 0) return curr > 0 ? "+100%" : "0%";
                            var diff = (curr - prev) / Math.Abs(prev) * 100;
                            var sign = diff > 0 ? "+" : "";
                            return $"{sign}{diff:N1}%";
                        };

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            
                            table.Cell().Text($"Оборот: {getDiffStr(currentIncome, prevIncome)}").SemiBold();
                            table.Cell().Text($"Витрати: {getDiffStr(currentExpenses, prevExpenses)}").SemiBold();
                            table.Cell().Text($"Прибуток: {getDiffStr(currentProfit, prevProfit)}").SemiBold();
                        });

                    });
                });
            }).GeneratePdf();

            var fileName = $"FinancialReport_{DateTime.Now:yyyy-MM-dd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        [HttpGet]
        public async Task<IActionResult> GetUserTips()
        {
            var userId = _userManager.GetUserId(User);
            var tips = await _analyticsService.CompareBusinessesForTipsAsync(userId);
            return Json(tips);
        }
        public class AnalyticsRequestDTO
        {
            public List<Guid> BusinessIds { get; set; } = new();
            public List<string> SelectedMetrics { get; set; } = new();
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
        public class GeneratePdfRequest
        {
            public List<Guid> BusinessIds { get; set; } = new();
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        public class TopItem
        {
            public string Business { get; set; }
            public string Product { get; set; }
            public string Category { get; set; }
        }
    }
}
