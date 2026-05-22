namespace BusinessAnalytics.Models.DTO
{
    public class CombinedAnalyticsDTO
    {
        public List<Guid> BusinessIds { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<BusinessMetricsDTO> Businesses { get; set; } = new();
        public Dictionary<Guid, List<ProfitTrendPoint>> ProfitTrends { get; set; } = new(); 
        public Dictionary<Guid, string> TopProducts { get; set; } = new(); 
        public Dictionary<Guid, string> TopCategories { get; set; } = new();
        public Dictionary<Guid, CategoryDistributionDTO> CategoryDistribution { get; set; } = new();

    }
    public class ProfitTrendPoint
    {
        public DateTime Date { get; set; }
        public decimal Profit { get; set; }
    }
    public class CategoryDistributionDTO
    {
        public string BusinessName { get; set; } = "";
        public Dictionary<string, decimal> Data { get; set; } = new();

        public static implicit operator Dictionary<object, object>(CategoryDistributionDTO v)
        {
            throw new NotImplementedException();
        }
    }
}
