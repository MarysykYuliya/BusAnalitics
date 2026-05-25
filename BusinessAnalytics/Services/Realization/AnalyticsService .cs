using BusinessAnalytics.Data;
using BusinessAnalytics.Models.DTO;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Models.Interfaces;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Services.Realization
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private const int SuggestionWindowDays = 30;
        public AnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<BusinessOverviewDTO>> GetUserBusinessOverviewsAsync(string userId)
        {
            var businesses = await _context.BusinessAccounts
                .Where(b => b.OwnerId == userId)
                .Include(b => b.Transactions)
                .ToListAsync();

            return businesses.Select(b => new BusinessOverviewDTO
            {
                BusinessId = b.Id,
                Name = b.Name,
                ImageUrl = b.LogoPath ?? "/images/default-business.png",
                Metrics = new Dictionary<string, decimal>
                {
                    ["Прибуток"] = b.Transactions
                        .Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount)
                        - b.Transactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount),
                    ["Оборот"] = b.Transactions
                        .Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount),
                    ["Витрати"] = b.Transactions
                        .Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount),
                }
            }).ToList();
        }

        public async Task<Dictionary<string, decimal>> GetMetricsForBusinessAsync(Guid businessId, DateTime startDate, DateTime endDate)
        {
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var transactions = await _context.Transactions
                .Include(t => t.Product)
                .ThenInclude(p => p.ProductCategory)
                .Where(t => t.BusinessAccountId == businessId && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            var income = transactions
                .Where(t => t.TransactionType == TransactionType.Income)
                .Sum(t => t.TotalAmount);

            var expenses = transactions
                .Where(t => t.TransactionType == TransactionType.Expense)
                .Sum(t => t.TotalAmount);

            var avgCheck = transactions
                .Where(t => t.TransactionType == TransactionType.Income)
                .DefaultIfEmpty()
                .Average(t => t == null ? 0 : (double)t.TotalAmount);

            var bestProduct = transactions
                .Where(t => t.TransactionType == TransactionType.Income && t.Product != null)
                .GroupBy(t => t.Product!.Name)
                .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
                .OrderByDescending(g => g.Total)
                .FirstOrDefault();

            var bestCategory = transactions
                .Where(t => t.TransactionType == TransactionType.Income && t.Product?.ProductCategory != null)
                .GroupBy(t => t.Product!.ProductCategory!.Name)
                .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
                .OrderByDescending(g => g.Total)
                .FirstOrDefault();

            var metrics = new Dictionary<string, decimal>
            {
                ["Прибуток"] = income - expenses,
                ["Оборот"] = income,
                ["Витрати"] = expenses,
                ["Середній чек"] = (decimal)avgCheck
            };

            if (bestProduct != null)
            {
                metrics["Найприбутковіший товар"] = bestProduct.Total;
                metrics[" це " + bestProduct.Name] = 0;
            }

            if (bestCategory != null)
                metrics["Найприбутковіша категорія"] = bestCategory.Total;

            return metrics;
        }

        public async Task<CombinedAnalyticsDTO> CompareBusinessesAsync(List<Guid> businessIds, DateTime startDate, DateTime endDate)
        {
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var businesses = await _context.BusinessAccounts
                .Where(b => businessIds.Contains(b.Id))
                .ToListAsync();

            var result = new CombinedAnalyticsDTO
            {
                BusinessIds = businessIds,
                StartDate = startDate,
                EndDate = endDate
            };

            foreach (var b in businesses)
            {
                var dist = await GetCategoryProfitDistributionAsync(b.Id, startDate, endDate);
                result.CategoryDistribution[b.Id] = new CategoryDistributionDTO
                {
                    BusinessName = b.Name,
                    Data = dist
                };
                var metrics = await GetMetricsForBusinessAsync(b.Id, startDate, endDate);

                result.Businesses.Add(new BusinessMetricsDTO
                {
                    BusinessId = b.Id,
                    BusinessName = b.Name,
                    Metrics = metrics
                });
                var trend = await _context.Transactions
                    .Where(t => t.BusinessAccountId == b.Id && t.Date >= startDate && t.Date <= endDate)
                    .GroupBy(t => t.Date.Date)
                    .Select(g => new ProfitTrendPoint
                    {
                        Date = g.Key,
                        Profit = g.Where(x => x.TransactionType == TransactionType.Income).Sum(x => x.TotalAmount)
                                - g.Where(x => x.TransactionType == TransactionType.Expense).Sum(x => x.TotalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                result.ProfitTrends[b.Id] = trend;
                var topProduct = await _context.Transactions
                    .Include(t => t.Product)
                    .Where(t => t.BusinessAccountId == b.Id
                             && t.TransactionType == TransactionType.Income
                             && t.Product != null
                             && t.Date >= startDate && t.Date <= endDate)
                    .GroupBy(t => t.Product!.Name)
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
                    .OrderByDescending(g => g.Total)
                    .FirstOrDefaultAsync();

                result.TopProducts[b.Id] = topProduct?.Name ?? "—";
                var topCategory = await _context.Transactions
                    .Include(t => t.Product)
                    .ThenInclude(p => p.ProductCategory)
                    .Where(t => t.BusinessAccountId == b.Id
                             && t.TransactionType == TransactionType.Income
                             && t.Product!.ProductCategory != null
                             && t.Date >= startDate && t.Date <= endDate)
                    .GroupBy(t => t.Product!.ProductCategory!.Name)
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
                    .OrderByDescending(g => g.Total)
                    .FirstOrDefaultAsync();

                result.TopCategories[b.Id] = topCategory?.Name ?? "—";
            }

            return result;
        }
        public async Task<Dictionary<string, decimal>> GetCategoryProfitDistributionAsync(Guid businessId, DateTime startDate, DateTime endDate)
        {
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var q = await _context.Transactions
                .Include(t => t.Product)
                .ThenInclude(p => p.ProductCategory)
                .Where(t => t.BusinessAccountId == businessId && t.Date >= startDate && t.Date <= endDate && t.TransactionType == TransactionType.Income)
                .Where(t => t.Product != null && t.Product.ProductCategory != null)
                .GroupBy(t => t.Product!.ProductCategory!.Name)
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.TotalAmount) })
                .ToListAsync();

            return q.ToDictionary(x => x.Category, x => x.Total);
        }

        private async Task<(decimal income, decimal expenses, decimal avgCheck, decimal profit)> ComputeBasicMetricsAsync(Guid businessId, DateTime startDate, DateTime endDate)
        {
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var trans = await _context.Transactions
                .Where(t => t.BusinessAccountId == businessId && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            var income = trans.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.TotalAmount);
            var expenses = trans.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.TotalAmount);

            var incomeTransactions = trans.Where(t => t.TransactionType == TransactionType.Income).ToList();
            double avgCheckDouble = incomeTransactions.Any()
                ? incomeTransactions.Average(t => (double)t.TotalAmount)
                : 0.0;

            var avgCheck = (decimal)avgCheckDouble;
            var profit = income - expenses;

            return (income, expenses, avgCheck, profit);
        }

        private async Task<decimal> ComputeProfitGrowthRateAsync(Guid businessId, DateTime currentStart, DateTime currentEnd)
        {
            var days = (currentEnd.Date - currentStart.Date).Days + 1;
            if (days <= 0) return 0m;

            var prevEnd = currentStart.AddDays(-1);
            var prevStart = prevEnd.AddDays(-days + 1);

            var (_, _, _, currentProfit) = await ComputeBasicMetricsAsync(businessId, currentStart, currentEnd);
            var (_, _, _, prevProfit) = await ComputeBasicMetricsAsync(businessId, prevStart, prevEnd);

            const decimal eps = 0.0001m;
            var denom = Math.Abs(prevProfit) + eps;
            var growth = (currentProfit - prevProfit) / denom * 100m;
            return Math.Round(growth, 2);
        }

        private async Task<(string topProductName, decimal sharePercent)> GetTopProductShareAsync(Guid businessId, DateTime startDate, DateTime endDate)
        {
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var incomeTransactions = await _context.Transactions
                .Include(t => t.Product)
                .Where(t => t.BusinessAccountId == businessId
                         && t.TransactionType == TransactionType.Income
                         && t.Product != null
                         && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            var totalIncome = incomeTransactions.Sum(t => t.TotalAmount);
            if (totalIncome <= 0) return ("—", 0m);

            var grouped = incomeTransactions
                .GroupBy(t => t.Product!.Name)
                .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
                .OrderByDescending(x => x.Total)
                .FirstOrDefault();

            if (grouped == null) return ("—", 0m);

            var share = grouped.Total / totalIncome * 100m;
            return (grouped.Name, Math.Round(share, 2));
        }

        public async Task<List<string>> GetSuggestionsAsync(string userId)
        {
            var businesses = await _context.BusinessAccounts
               .Where(b => b.OwnerId == userId)
               .ToListAsync();

            var tips = new List<string>();

            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-SuggestionWindowDays + 1);

            foreach (var b in businesses)
            {
                var (income, expenses, avgCheck, profit) = await ComputeBasicMetricsAsync(b.Id, start, end);
                var growthRate = await ComputeProfitGrowthRateAsync(b.Id, start, end);
                var (topProductName, topProductShare) = await GetTopProductShareAsync(b.Id, start, end);

                if (profit < 0)
                    tips.Add($"Бізнес '{b.Name}': прибуток від'ємний ({profit:0.00}₴) за останні {SuggestionWindowDays} днів — переглянь витрати і ціни.");


                if (avgCheck > 0 && avgCheck < 100)
                    tips.Add($"Бізнес '{b.Name}': середній чек лише {avgCheck:0.00}₴ — розглянь акції по апсейлу або набори для підвищення середнього чеку.");

                if (growthRate < -25)
                    tips.Add($"Бізнес '{b.Name}': прибуток впав на {growthRate:0.##}% порівняно з попереднім періодом — потрібний аналіз продажів і маркетингу.");


                if (topProductShare >= 60)
                    tips.Add($"Бізнес '{b.Name}': топ-продукт '{topProductName}' генерує {topProductShare:0.##}% доходу — ризик залежності. Дивись диверсифікацію асортименту.");


                if (income > 0 && expenses / income > 0.7m) // >70% витрат
                    tips.Add($"Бізнес '{b.Name}': витрати складають {(expenses / (income + 0.0001m) * 100m):0.##}% від обороту — оптимізуй витрати або підвищуй маржу.");

                if (income == 0 && expenses == 0)
                    tips.Add($"Бізнес '{b.Name}': немає транзакцій за останні {SuggestionWindowDays} днів — почни вносити щоденні звіти або підключи синхронізацію.");
            }

            if (!tips.Any())
                tips.Add("Усі бізнеси в межах норми за останні 30 днів. Продовжуйте в тому ж дусі!");

            return tips;
        }
        public async Task<CombinedAnalyticsDTO> CompareBusinessesForTipsAsync(List<Guid> businessIds, DateTime startDate, DateTime endDate)
        {
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var businesses = await _context.BusinessAccounts
                .Where(b => businessIds.Contains(b.Id))
                .ToListAsync();

            var result = new CombinedAnalyticsDTO
            {
                BusinessIds = businessIds,
                StartDate = startDate,
                EndDate = endDate
            };

            foreach (var b in businesses)
            {
                var metrics = await GetMetricsForBusinessAsync(b.Id, startDate, endDate);

                result.Businesses.Add(new BusinessMetricsDTO
                {
                    BusinessId = b.Id,
                    BusinessName = b.Name,
                    Metrics = metrics
                });

                var trend = await _context.Transactions
                    .Where(t => t.BusinessAccountId == b.Id && t.Date >= startDate && t.Date <= endDate)
                    .GroupBy(t => t.Date.Date)
                    .Select(g => new ProfitTrendPoint
                    {
                        Date = g.Key,
                        Profit = g.Where(x => x.TransactionType == TransactionType.Income).Sum(x => x.TotalAmount)
                                - g.Where(x => x.TransactionType == TransactionType.Expense).Sum(x => x.TotalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                result.ProfitTrends[b.Id] = trend;

                var topProduct = await _context.Transactions
                    .Include(t => t.Product)
                    .Where(t => t.BusinessAccountId == b.Id
                             && t.TransactionType == TransactionType.Income
                             && t.Product != null
                             && t.Date >= startDate && t.Date <= endDate)
                    .GroupBy(t => t.Product!.Name)
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
                    .OrderByDescending(g => g.Total)
                    .FirstOrDefaultAsync();

                result.TopProducts[b.Id] = topProduct?.Name ?? "—";

                var topCategory = await _context.Transactions
                    .Include(t => t.Product)
                    .ThenInclude(p => p.ProductCategory)
                    .Where(t => t.BusinessAccountId == b.Id
                             && t.TransactionType == TransactionType.Income
                             && t.Product!.ProductCategory != null
                             && t.Date >= startDate && t.Date <= endDate)
                    .GroupBy(t => t.Product!.ProductCategory!.Name)
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.TotalAmount) })
                    .OrderByDescending(g => g.Total)
                    .FirstOrDefaultAsync();

                result.TopCategories[b.Id] = topCategory?.Name ?? "—";
            }

            return result;
        }
        public async Task<List<string>> CompareBusinessesForTipsAsync(string userId)
        {
            var businesses = await _context.BusinessAccounts
                .Where(b => b.OwnerId == userId)
                .Include(b => b.Transactions)
                .ToListAsync();

            var startDate = DateTime.UtcNow.AddDays(-30);
            var endDate = DateTime.UtcNow;

            var tips = new List<string>();

            foreach (var b in businesses)
            {
                var metrics = await GetMetricsForBusinessAsync(b.Id, startDate, endDate);

                decimal profit = metrics.ContainsKey("Прибуток") ? metrics["Прибуток"] : 0;
                decimal turnover = metrics.ContainsKey("Оборот") ? metrics["Оборот"] : 0;
                decimal expenses = metrics.ContainsKey("Витрати") ? metrics["Витрати"] : 0;
                if (profit < 0)
                    tips.Add($"Бізнес «{b.Name}» має від’ємний прибуток. Переглянь витрати.");

                if (turnover > 0 && expenses / turnover > 0.7m)
                    tips.Add($"Бізнес «{b.Name}» має високі витрати ({Math.Round(expenses / turnover * 100)}% від обороту).");

                if (metrics.ContainsKey("Середній чек") && metrics["Середній чек"] < 100)
                    tips.Add($"У бізнесу «{b.Name}» середній чек менше 100₴ — подумай про акції або апсейлінг.");

                if (metrics.ContainsKey("Найприбутковіший товар"))
                {
                    var key = metrics.Keys.FirstOrDefault(k => k.Contains(" це "));
                    tips.Add($"У бізнесу «{b.Name}» є найприбутковіший товар {key} — {metrics["Найприбутковіший товар"]}₴ продажів.");
                    
                }
                var trend = await _context.Transactions
                    .Where(t => t.BusinessAccountId == b.Id && t.Date >= startDate && t.Date <= endDate)
                    .GroupBy(t => t.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Profit = g.Where(x => x.TransactionType == TransactionType.Income).Sum(x => x.TotalAmount)
                                - g.Where(x => x.TransactionType == TransactionType.Expense).Sum(x => x.TotalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                if (trend.Count > 1)
                {
                    var first = trend.First().Profit;
                    var last = trend.Last().Profit;

                    if (first != 0)
                    {
                        var changePercent = Math.Round((last / first - 1) * 100, 1);

                        if (changePercent > 10)
                        {
                            tips.Add($"Прибуток бізнесу «{b.Name}» зріс на {changePercent}%  — чудовий результат!");
                        }
                        else if (changePercent < -10)
                        {
                            tips.Add($"Прибуток бізнесу «{b.Name}» знизився на {Math.Abs(changePercent)}%  — варто проаналізувати причини.");
                        }
                    }
                }
            }

            if (!tips.Any())
                tips.Add("Усі бізнеси стабільні! Продовжуй у тому ж дусі.");

            return tips;
        }

    }
}
