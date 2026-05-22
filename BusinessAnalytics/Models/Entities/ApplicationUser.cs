using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BusinessAnalytics.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Повне ім'я")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Компанія / Організація")]
        public string? Organization { get; set; }

        [DataType(DataType.MultilineText)]
        [Display(Name = "Про себе")]
        public string? About { get; set; }
        public ICollection<BusinessAccount>? BusinessAccounts { get; set; }

        /// <summary>Telegram Chat ID for daily report delivery.</summary>
        public string? TelegramChatId { get; set; }

        /// <summary>One-time token for Telegram deep-link auth.</summary>
        public string? TelegramLinkToken { get; set; }

        /// <summary>Expiry for the link token (15 minutes).</summary>
        public DateTime? TelegramLinkTokenExpiry { get; set; }
    }
}
