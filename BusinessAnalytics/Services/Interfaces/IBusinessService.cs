using BusinessAnalytics.Models.Entities;

namespace BusinessAnalytics.Services.Interfaces
{
    public interface IBusinessService
    {
        Task<List<BusinessAccount>> GetUserBusinessesAsync(string userId);
        Task<BusinessAccount?> GetBusinessByIdAsync(Guid id, string userId);
        Task CreateBusinessAsync(BusinessAccount business);
        Task UpdateBusinessAsync(BusinessAccount business);
        Task DeleteBusinessAsync(Guid id, string userId);
        Task<List<BusinessAccount>> GetBusinessesByUserAsync(string userId);

    }
}
