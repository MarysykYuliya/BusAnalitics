namespace BusinessAnalytics.Models.DTO
{
    public class BusinessMetricsDTO
    {
        public Guid BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public Dictionary<string, decimal> Metrics { get; set; } = new();
    }
}
