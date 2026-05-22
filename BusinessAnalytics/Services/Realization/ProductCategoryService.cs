using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Services.Realization
{
    public class ProductCategoryService : IProductCategoryService
    {
        private readonly ApplicationDbContext _context;

        public ProductCategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductCategory>> GetCategoriesByBusinessAsync(Guid businessId)
        {
            return await _context.ProductCategories
                .Where(c => c.BusinessAccountId == businessId)
                .ToListAsync();
        }

        public async Task<ProductCategory?> GetCategoryByIdAsync(Guid categoryId, Guid businessId)
        {
            return await _context.ProductCategories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.BusinessAccountId == businessId);
        }

        public async Task CreateCategoryAsync(ProductCategory category, Guid businessId)
        {
            category.BusinessAccountId = businessId;
            _context.ProductCategories.Add(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCategoryAsync(ProductCategory category)
        {
            var category1 = await _context.ProductCategories
        .FirstOrDefaultAsync(c => c.Id == category.Id);

            if (category1 == null)
                throw new Exception("Категорія не знайдена");
            category1.Name = category.Name;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(Guid categoryId, Guid businessId)
        {
            var category = await _context.ProductCategories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.BusinessAccountId == businessId);

            if (category != null)
            {
                _context.ProductCategories.Remove(category);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Product>> GetProductsByCategoryAsync(Guid businessId, Guid categoryId)
        {
            return await _context.Products
                .Where(p => p.BusinessAccountId == businessId && p.ProductCategoryId == categoryId)
                .ToListAsync();
        }
    }
}