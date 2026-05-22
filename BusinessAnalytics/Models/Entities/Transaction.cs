using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessAnalytics.Models.Entities
{
    public enum TransactionType
    {
        Income = 1,
        Expense = 2
    }

    public class Transaction
    {
        public Guid Id { get; set; }

        [Required]
        public Guid BusinessAccountId { get; set; }
        public BusinessAccount BusinessAccount { get; set; } = null!;

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        public TransactionType TransactionType { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public int? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }

        [Required]
        public decimal TotalAmount { get; set; }

        public string? Description { get; set; }
        public Guid? ExpenseCategoryId { get; set; }
        public ExpenseCategory? ExpenseCategory { get; set; }
        
        public Guid? ImportBatchId { get; set; }
    }
}
