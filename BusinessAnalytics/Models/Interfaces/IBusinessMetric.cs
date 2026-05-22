namespace BusinessAnalytics.Models.Interfaces
{
    public interface IBusinessMetric
    {
        string Name { get; } 
        string DisplayName { get; } 
        Task<decimal> CalculateAsync(Guid businessId, DateTime startDate, DateTime endDate);
    }
}
