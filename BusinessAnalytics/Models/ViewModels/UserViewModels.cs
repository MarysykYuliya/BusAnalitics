namespace BusinessAnalytics.Models.ViewModels
{
    using System.ComponentModel.DataAnnotations;

    namespace BusinessAnalytics.Models.ViewModels
    {
        public class RegisterViewModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Електронна пошта")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Пароль")]
            public string Password { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Підтвердження пароля")]
            [Compare("Password", ErrorMessage = "Паролі не збігаються")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public class LoginViewModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Електронна пошта")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Пароль")]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Запам’ятати мене")]
            public bool RememberMe { get; set; }
        }
    }

}
