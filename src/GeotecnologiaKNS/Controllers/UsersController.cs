using GeotecnologiaKNS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(
            ApplicationDbContext context
            , IPasswordHasher<ApplicationUser> passwordHasher
            , UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _userManager = userManager;
        }

        // GET: User
        [Authorize(Policy = "UserCanUserCreate")]
        public async Task<IActionResult> Index()
        {
            return _context.Users != null ?
                        View(await _context.Users.ToListAsync()) :
                        Problem("Entity set 'ApplicationDbContext.Users'  is null.");
        }

        // GET: Users/Create
        [Authorize(Policy = "UserCanUserCreate")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Create(UserViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var user = viewModel.ToModel();
            _context.Users.Add(user);
            var hashedPassword = _passwordHasher.HashPassword(user, viewModel.Password);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.PasswordHash = hashedPassword;

            _context.SaveChanges();

            await _userManager.AddToRoleAsync(user, viewModel.Role);

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/5
        [Authorize(Policy = "UserCanUserCreate")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user.ToViewModel());
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserViewModel user)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserViewModelExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
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

        private bool UserViewModelExists(string id)
        {
            return (_context.Users?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
