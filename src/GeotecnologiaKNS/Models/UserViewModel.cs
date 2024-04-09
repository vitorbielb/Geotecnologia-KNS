using System.ComponentModel.DataAnnotations;

namespace GeotecnologiaKNS.Models
{
    public class UserViewModel
    {
        private const string RequiredMessage = "Campo obrigatório";
        private const string PasswordLengthMessage = "A {0} deve ter no mínimo {2} e no máximo {1} caracteres.";
        private const string PasswordCompareMessage = "A senha e a confirmação de senha não coincidem.";

        public string? Id { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Nome")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Celular")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = PasswordLengthMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "Confirme a senha")]
        [Compare(nameof(Password), ErrorMessage = PasswordCompareMessage)]
        public string ConfirmPassword { get; set; } = string.Empty;

        public int TenantId { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Função")]
        public string Role { get; set; } = string.Empty;
    }

    public static class UserViewModelExtensions
    {
        public static ApplicationUser ToModel(this UserViewModel viewModel)
        {
            var userName = viewModel.UserName?.Trim() ?? string.Empty;
            var email = viewModel.Email?.Trim() ?? string.Empty;
            var phoneNumber = viewModel.PhoneNumber?.Trim();

            return new ApplicationUser
            {
                Id = viewModel.Id,
                UserName = userName,
                Email = email,
                PhoneNumber = phoneNumber,
                TenantId = viewModel.TenantId,
                NormalizedUserName = userName.ToUpperInvariant(),
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true
            };
        }

        public static UserViewModel ToViewModel(this ApplicationUser model)
        {
            return new UserViewModel
            {
                Id = model.Id,
                UserName = model.UserName ?? string.Empty,
                Email = model.Email ?? string.Empty,
                PhoneNumber = model.PhoneNumber ?? string.Empty,
                TenantId = model.TenantId
            };
        }
    }
}