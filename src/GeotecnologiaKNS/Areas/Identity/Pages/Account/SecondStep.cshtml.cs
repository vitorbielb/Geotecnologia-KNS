using GeotecnologiaKNS.Infra;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace GeotecnologiaKNS.Areas.Identity.Pages.Account
{
    public class SecondStepModel : PageModel
    {
        private readonly ILogger<SecondStepModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

        public SecondStepModel(
            ILogger<SecondStepModel> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPasswordHasher<ApplicationUser> passwordHasher)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _passwordHasher = passwordHasher;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [Display(Name = "Nome de usuario")]
            public string UserName { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            public int TenantId { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }


        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await _context.Users.AnyAsync())
            {
                return RedirectToPage("./Login");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = CreateUser();
            _context.Users.Add(user);

            var hashedPassword = _passwordHasher.HashPassword(user, Input.Password);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.PasswordHash = hashedPassword;

            _context.SaveChanges();

            await _userManager.AddToRoleAsync(user, nameof(Roles.Administrador));

            return RedirectToPage("./Login");
        }

        private ApplicationUser CreateUser()
        {
            var user = Activator.CreateInstance<ApplicationUser>();
            user.Email = Input.Email;
            user.EmailConfirmed = true;
            user.UserName = Input.UserName;
            user.NormalizedUserName = Input.UserName.Normalize();
            user.TenantId = Input.TenantId;
            return user;
        }
    }
}

