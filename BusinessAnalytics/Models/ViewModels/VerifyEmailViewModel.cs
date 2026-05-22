using System.ComponentModel.DataAnnotations;

namespace BusinessAnalytics.Models.ViewModels
{
    public class VerifyEmailViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть код підтвердження")]
        [Display(Name = "Код підтвердження")]
        public string Code { get; set; } = string.Empty;
    }
}
