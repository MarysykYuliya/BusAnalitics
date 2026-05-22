using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Services.Realization
{
    public class BusinessService : IBusinessService
    {
        private readonly ApplicationDbContext _context;

        public BusinessService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<BusinessAccount>> GetUserBusinessesAsync(string userId)
        {
            return await _context.BusinessAccounts
                                 .Where(b => b.OwnerId == userId)
                                 .ToListAsync();
        }

        public async Task<BusinessAccount?> GetBusinessByIdAsync(Guid id, string userId)
        {
            return await _context.BusinessAccounts
                                 .Include(b => b.Products)
                                 .Include(b => b.ProductCategories)
                                 .Include(b => b.Transactions)
                                 .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == userId);
        }

        public async Task CreateBusinessAsync(BusinessAccount business)
        {
            _context.BusinessAccounts.Add(business);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateBusinessAsync(BusinessAccount business)
        {
            _context.BusinessAccounts.Update(business);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteBusinessAsync(Guid id, string userId)
        {
            var business = await _context.BusinessAccounts
                                         .FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == userId);
            if (business != null)
            {
                _context.BusinessAccounts.Remove(business);
                await _context.SaveChangesAsync();
            }
        }
        public async Task<List<BusinessAccount>> GetBusinessesByUserAsync(string userId)
        {
            return await _context.BusinessAccounts
                .Where(b => b.OwnerId == userId)
                .ToListAsync();
        }
    }
}
