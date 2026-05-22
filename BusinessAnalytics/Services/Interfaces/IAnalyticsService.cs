using BusinessAnalytics.Models.DTO;

namespace BusinessAnalytics.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<List<BusinessOverviewDTO>> GetUserBusinessOverviewsAsync(string userId);
        Task<Dictionary<string, decimal>> GetMetricsForBusinessAsync(Guid businessId, DateTime startDate, DateTime endDate);
        Task<CombinedAnalyticsDTO> CompareBusinessesAsync(List<Guid> businessIds, DateTime startDate, DateTime endDate);
        Task<CombinedAnalyticsDTO> CompareBusinessesForTipsAsync(List<Guid> businessIds, DateTime startDate, DateTime endDate);
        Task<List<string>> CompareBusinessesForTipsAsync(string userId);
        Task<List<string>> GetSuggestionsAsync(string userId);
    }
}
