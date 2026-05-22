using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessAnalytics.Models.Entities
{
    public class ProductCategory
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

        [StringLength(200)]
        [Display(Name = "Опис категорії")]
        public string? Description { get; set; }

        [Display(Name = "Колір (опціонально, для UI)")]
        public string? ColorHex { get; set; }

        public ICollection<Product>? Products { get; set; }
    }
}
