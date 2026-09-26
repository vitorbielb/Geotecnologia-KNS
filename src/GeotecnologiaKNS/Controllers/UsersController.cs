using GeotecnologiaKNS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize(Policy = "UserCanUserCreate")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserContext _userContext;

        public UsersController(
            ApplicationDbContext context
            , IPasswordHasher<ApplicationUser> passwordHasher
            , UserManager<ApplicationUser> userManager
            , IUserContext userContext)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _userManager = userManager;
            _userContext = userContext;
        }

        // GET: User
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Create(UserViewModel viewModel)
        {
            // Somente o administrador da aplicação escolhe a indústria do usuário.
            // Para os demais, o tenant vem sempre do usuário autenticado.
            if (!_userContext.IsApplicationAdmin)
            {
                viewModel.TenantId = _userContext.TenantId ?? 0;
            }

            if (viewModel.TenantId <= 0)
            {
                ModelState.AddModelError(nameof(UserViewModel.TenantId), "Indústria inválida.");
            }

            if (await _context.Users.AnyAsync(x => x.NormalizedEmail == viewModel.Email.Trim().ToUpperInvariant()))
            {
                ModelState.AddModelError(nameof(UserViewModel.Email), "Já existe um usuário com este email.");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var user = viewModel.ToModel();
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.PasswordHash = _passwordHasher.HashPassword(user, viewModel.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _userManager.AddToRoleAsync(user, viewModel.Role);

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            var user = await FindUserAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user.ToViewModel());
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            // Busca pelo DbSet (e não por Find) para que o filtro de tenant seja aplicado:
            // um administrador de cliente não deve alterar usuários de outra indústria.
            var user = await FindUserAsync(viewModel.Id);

            if (user == null)
            {
                return NotFound();
            }

            user.UserName = viewModel.UserName.Trim();
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            user.Email = viewModel.Email.Trim();
            user.NormalizedEmail = user.Email.ToUpperInvariant();
            user.PhoneNumber = viewModel.PhoneNumber?.Trim();
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.PasswordHash = _passwordHasher.HashPassword(user, viewModel.Password);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> GetRolesAsync()
        {
            var roles = await _context.Roles.ToListAsync();

            var selectListItems = roles.ToSelectListItems(
                       x => x.ToString(),
                       x => x.Id,
                       options => options.Placeholder = "Selecione...");

            return Ok(selectListItems);
        }

        private async Task<ApplicationUser?> FindUserAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
