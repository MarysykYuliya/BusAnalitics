namespace BusinessAnalytics.Models.DTO
{
    public class BusinessOverviewDTO
    {
        public Guid BusinessId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public Dictionary<string, decimal> Metrics { get; set; }
    }
}
