using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Services.Realization
{
    public class TransactionService : ITransactionService
    {
        private readonly ApplicationDbContext _context;

        public TransactionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Transaction>> GetTransactionsAsync(Guid businessId)
        {
            return await _context.Transactions
                .Where(t => t.BusinessAccountId == businessId)
                .Include(t => t.Product)
                .Include(t => t.ExpenseCategory)
                .ToListAsync();
        }

        public async Task<Transaction?> GetTransactionByIdAsync(Guid transactionId, Guid businessId)
        {
            return await _context.Transactions
                .Include(t => t.Product)
                .Include(t => t.ExpenseCategory)
                .FirstOrDefaultAsync(t => t.Id == transactionId && t.BusinessAccountId == businessId);
        }

        public async Task CreateTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTransactionAsync(Guid transactionId, Guid businessId)
        {
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == transactionId && t.BusinessAccountId == businessId);

            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }
        }
    }
}
