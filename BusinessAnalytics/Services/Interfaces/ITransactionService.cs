using BusinessAnalytics.Models.Entities;

namespace BusinessAnalytics.Services.Interfaces
{
    public interface ITransactionService
    {
        Task<List<Transaction>> GetTransactionsAsync(Guid businessId);
        Task<Transaction?> GetTransactionByIdAsync(Guid transactionId, Guid businessId);
        Task CreateTransactionAsync(Transaction transaction);
        Task UpdateTransactionAsync(Transaction transaction);
        Task DeleteTransactionAsync(Guid transactionId, Guid businessId);
    }
}
