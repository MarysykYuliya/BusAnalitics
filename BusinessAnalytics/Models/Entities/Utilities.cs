using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessAnalytics.Models.Entities
{
    /// <summary>Фізичне приміщення бізнесу (офіс, склад, магазин тощо)</summary>
    public class Premises
    {
        [Key]
        public Guid Id { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Назва приміщення")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        [Display(Name = "Адреса")]
        public string? Address { get; set; }

        [StringLength(500)]
        [Display(Name = "Опис")]
        public string? Description { get; set; }

        [Required]
        public Guid BusinessAccountId { get; set; }
        public BusinessAccount BusinessAccount { get; set; } = null!;

        public ICollection<UtilityRecord> UtilityRecords { get; set; } = new List<UtilityRecord>();
    }

    /// <summary>Тип комунальної послуги (Електрика, Газ, Вода...)</summary>
    public class UtilityType
    {
        [Key]
        public Guid Id { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Назва послуги")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public Guid BusinessAccountId { get; set; }
        public BusinessAccount BusinessAccount { get; set; } = null!;

        public ICollection<UtilityRecord> UtilityRecords { get; set; } = new List<UtilityRecord>();
    }

    /// <summary>Щомісячний запис оплати конкретної послуги для конкретного приміщення</summary>
    public class UtilityRecord
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid PremisesId { get; set; }
        public Premises Premises { get; set; } = null!;

        [Required]
        public Guid UtilityTypeId { get; set; }
        public UtilityType UtilityType { get; set; } = null!;

        [Required]
        [Display(Name = "Рік")]
        public int Year { get; set; }

        [Required]
        [Range(1, 12)]
        [Display(Name = "Місяць")]
        public int Month { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Сума")]
        public decimal Amount { get; set; }

        [Display(Name = "Коментар")]
        [StringLength(500)]
        public string? Note { get; set; }

        [Display(Name = "Оплачено")]
        public bool IsPaid { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }

        /// <summary>Посилання на автоматично створену транзакцію при оплаті</summary>
        public Guid? LinkedTransactionId { get; set; }
    }
}
