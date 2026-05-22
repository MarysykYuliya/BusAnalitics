using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Models.Metrics
{
    public class ProfitMetric : IBusinessMetric
    {
        private readonly ApplicationDbContext _context;

        public ProfitMetric(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Name => "Profit";
        public string DisplayName => "Прибуток";

        public async Task<decimal> CalculateAsync(Guid businessId, DateTime startDate, DateTime endDate)
        {
            var income = await _context.Transactions
                .Where(t => t.BusinessAccountId == businessId && t.Date >= startDate && t.Date <= endDate && t.TransactionType == TransactionType.Income)
                .SumAsync(t => t.TotalAmount);

            var expenses = await _context.Transactions
                .Where(t => t.BusinessAccountId == businessId && t.Date >= startDate && t.Date <= endDate && t.TransactionType == TransactionType.Expense)
                .SumAsync(t => t.TotalAmount);

            return income - expenses;
        }
    }
}
