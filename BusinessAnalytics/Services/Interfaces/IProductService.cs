using BusinessAnalytics.Models.Entities;

namespace BusinessAnalytics.Services.Interfaces
{
    public interface IProductService
    {
        Task<List<Product>> GetProductsAsync(Guid businessId);
        Task<Product?> GetProductByIdAsync(Guid productId, Guid businessId);
        Task CreateProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(Guid productId, Guid businessId);
    }
}
