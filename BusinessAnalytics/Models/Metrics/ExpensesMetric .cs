using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Models.Metrics
{
    public class ExpensesMetric : IBusinessMetric
    {
        private readonly ApplicationDbContext _context;

        public ExpensesMetric(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Name => "Expenses";
        public string DisplayName => "Витрати";

        public async Task<decimal> CalculateAsync(Guid businessId, DateTime startDate, DateTime endDate)
        {
            return await _context.Transactions
                .Where(t => t.BusinessAccountId == businessId && t.Date >= startDate && t.Date <= endDate && t.TransactionType == TransactionType.Expense)
                .SumAsync(t => t.TotalAmount);
        }
    }
}
