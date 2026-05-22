using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessAnalytics.Models.Entities
{
    public class BusinessAccount
    {
        [Key]
        public Guid Id { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Назва бізнесу")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        [Display(Name = "Опис бізнесу")]
        public string? Description { get; set; }

        [StringLength(200)]
        [Display(Name = "Адреса")]
        public string? Address { get; set; }

        [StringLength(100)]
        [Display(Name = "Контактна особа")]
        public string? ContactPerson { get; set; }

        [StringLength(20)]
        [Phone]
        [Display(Name = "Телефон")]
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [StringLength(20)]
        [Display(Name = "Код ЄДРПОУ / ІПН")]
        public string? RegistrationCode { get; set; }

        [Display(Name = "Дата створення")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(300)]
        [Display(Name = "Логотип бізнесу")]
        public string? LogoPath { get; set; }
        // Зв’язки
        [Required]
        [ForeignKey(nameof(Owner))]
        public string OwnerId { get; set; } = string.Empty;
        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<ProductCategory> ProductCategories { get; set; } 
        public ICollection<Product>? Products { get; set; }
        public ICollection<Transaction>? Transactions { get; set; }
        public ICollection<ExpenseCategory>? ExpenseCategories { get; set; }
        public ICollection<Premises>? Premises { get; set; }
        public ICollection<UtilityType>? UtilityTypes { get; set; }
    }
}
