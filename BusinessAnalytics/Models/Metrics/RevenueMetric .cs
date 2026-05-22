using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Models.Metrics
{
    public class RevenueMetric : IBusinessMetric
    {
        private readonly ApplicationDbContext _context;

        public RevenueMetric(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Name => "Revenue";
        public string DisplayName => "Оборот";

        public async Task<decimal> CalculateAsync(Guid businessId, DateTime startDate, DateTime endDate)
        {
            return await _context.Transactions
                .Where(t => t.BusinessAccountId == businessId && t.Date >= startDate && t.Date <= endDate)
                .SumAsync(t => t.TotalAmount);
        }
    }
}
