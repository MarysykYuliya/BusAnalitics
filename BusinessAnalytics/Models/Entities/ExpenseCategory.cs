using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessAnalytics.Models.Entities
{
    public class ExpenseCategory
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [ForeignKey(nameof(BusinessAccount))]
        public Guid BusinessAccountId { get; set; }
        public BusinessAccount BusinessAccount { get; set; } = null!;

        [Required, StringLength(100)]
        [Display(Name = "Назва категорії")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Transaction>? Transactions { get; set; }
    }
}
