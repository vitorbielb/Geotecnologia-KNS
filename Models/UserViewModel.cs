using System.ComponentModel.DataAnnotations;

namespace GeotecnologiaKNS.Models
{
    public class UserViewModel
    {
        [Key]
        public string Id { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Nome")]
        public string UserName { get; set; }

        [Required]
        [Display(Name = "Celular")]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirme senha")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public int TenantId { get; set; }

        [Required]
        [Display(Name = "Função")]
        public string Role { get; set; }
    }

    public static class UserViewModelExtensions
    {
        public static ApplicationUser ToModel(this UserViewModel viewModel) => new()
        {
            Id = viewModel.Id,
            PhoneNumber = viewModel.PhoneNumber,
            UserName = viewModel.UserName,
            Email = viewModel.Email,
            TenantId = viewModel.TenantId,
            NormalizedUserName = viewModel.UserName.Normalize(),
            NormalizedEmail = viewModel.Email.Normalize(),
            EmailConfirmed = true,
        };

        public static UserViewModel ToViewModel(this ApplicationUser model) => new()
        {
            Id = model.Id,
            PhoneNumber = model.PhoneNumber,
            UserName = model.UserName,
            Email = model.Email,
            TenantId = model.TenantId
        };
    }
}
