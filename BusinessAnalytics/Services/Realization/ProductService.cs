using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Services.Realization
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;

        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetProductsAsync(Guid businessId)
        {
            return await _context.Products
                .Where(p => p.BusinessAccountId == businessId)
                .Include(p => p.ProductCategory)
                .ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(Guid productId, Guid businessId)
        {
            return await _context.Products
                .Include(p => p.ProductCategory)
                .FirstOrDefaultAsync(p => p.Id == productId && p.BusinessAccountId == businessId);
        }

        public async Task CreateProductAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateProductAsync(Product product)
        {
            var existingProduct = await _context.Products
        .FirstOrDefaultAsync(p => p.Id == product.Id);

            if (existingProduct == null)
                throw new Exception("Продукт не знайдено");
            existingProduct.Name = product.Name;
            existingProduct.CostPrice = product.CostPrice;
            existingProduct.ProductCategoryId = product.ProductCategoryId; 
            existingProduct.MarkupPercent = product.MarkupPercent;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteProductAsync(Guid productId, Guid businessId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.BusinessAccountId == businessId);

            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }
    }
}
