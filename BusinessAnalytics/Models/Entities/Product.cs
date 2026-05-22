using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessAnalytics.Models.Entities
{
    public class Product
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [ForeignKey(nameof(BusinessAccount))]
        public Guid BusinessAccountId { get; set; }
        public BusinessAccount BusinessAccount { get; set; } = null!;

        [Required, StringLength(100)]
        [Display(Name = "Назва товару / послуги")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Тип (товар / послуга)")]
        public string? Type { get; set; }

        [ForeignKey(nameof(ProductCategory))]
        [Display(Name = "Категорія товару")]
        public Guid? ProductCategoryId { get; set; }
        public ProductCategory? ProductCategory { get; set; }

        [Range(0.01, double.MaxValue)]
        [Display(Name = "Собівартість")]
        public decimal CostPrice { get; set; }

        [Range(0, 1000)]
        [Display(Name = "Націнка (%)")]
        public decimal MarkupPercent { get; set; }

        [NotMapped]
        [Display(Name = "Кінцева ціна")]
        public decimal FinalPrice => CostPrice + (CostPrice * MarkupPercent / 100);
    }
}
