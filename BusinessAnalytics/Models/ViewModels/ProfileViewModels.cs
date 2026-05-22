using System.ComponentModel.DataAnnotations;

namespace BusinessAnalytics.Models.ViewModels
{
    public class ProfileViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? TelegramChatId { get; set; }
    }

    public class ChangePasswordProfileViewModel
    {
        [Required(ErrorMessage = "Введіть поточний пароль")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть новий пароль")]
        [MinLength(6, ErrorMessage = "Пароль має бути не менше 6 символів")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Підтвердіть пароль")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Паролі не збігаються")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ChangeEmailViewModel
    {
        [Required(ErrorMessage = "Введіть нову пошту")]
        [EmailAddress(ErrorMessage = "Невірний формат email")]
        public string NewEmail { get; set; } = string.Empty;
    }

    public class ConfirmEmailChangeViewModel
    {
        public string NewEmail { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
