using BusinessAnalytics.Models.Entities;

namespace BusinessAnalytics.Services.Interfaces
{
    public interface IProductCategoryService
    {
        Task<List<ProductCategory>> GetCategoriesByBusinessAsync(Guid businessId);
        Task<ProductCategory?> GetCategoryByIdAsync(Guid categoryId, Guid businessId);
        Task CreateCategoryAsync(ProductCategory category, Guid businessId);
        Task UpdateCategoryAsync(ProductCategory category);
        Task DeleteCategoryAsync(Guid categoryId, Guid businessId);
        Task<List<Product>> GetProductsByCategoryAsync(Guid businessId, Guid categoryId);
    }
}
