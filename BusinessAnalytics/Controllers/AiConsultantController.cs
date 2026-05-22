using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BusinessAnalytics.Services.Interfaces;
using BusinessAnalytics.Models.Entities;

using BusinessAnalytics.Data;

using Microsoft.AspNetCore.Authorization;

namespace BusinessAnalytics.Controllers
{
    [Authorize(Roles = "ProUser")]
    public class AiConsultantController : Controller
    {
        private readonly IAiService _aiService;
        private readonly ApplicationDbContext _context; 

        public AiConsultantController(IAiService aiService, ApplicationDbContext context)
        {
            _aiService = aiService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ViewBag.Businesses = await _context.BusinessAccounts
                .Where(b => b.OwnerId == userId)
                .ToListAsync();
                
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Generate(Guid businessId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ViewBag.Businesses = await _context.BusinessAccounts
                .Where(b => b.OwnerId == userId)
                .ToListAsync();
                
            ViewBag.SelectedBusinessId = businessId;

            var transactions = await _context.Transactions
                .Include(t => t.BusinessAccount)
                .Where(t => t.BusinessAccountId == businessId && t.BusinessAccount.OwnerId == userId) 
                .AsNoTracking()
                .ToListAsync();

            if (!transactions.Any())
            {
                ViewBag.Report = "<b>Немає даних:</b> У цьому бізнесі ще немає жодної транзакції. Додайте їх та спробуйте знову!";
                return View("Index");
            }

            var businessName = transactions.First().BusinessAccount.Name;
            
            decimal income = transactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount);
            decimal expense = transactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount);
            int transactionCount = transactions.Count;

            string topCategory = transactions
                .Where(t => !string.IsNullOrEmpty(t.Description))
                .GroupBy(t => t.Description)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Різне";

            var report = await _aiService.GenerateFinancialReportAsync(businessName, income, expense, transactionCount, topCategory);

            if (report.Contains("ServiceUnavailable") || report.Contains("UNAVAILABLE") || report.Contains("503"))
            {
                ViewBag.ReportError = "unavailable";
            }
            else if (report.Contains("\"error\"") || report.Contains("Помилка від Google"))
            {
                ViewBag.ReportError = "generic";
            }
            else
            {
                ViewBag.Report = report;
            }
            return View("Index");
        }
    }
}