using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = await _context.Users.Include(x => x.Industria).ToListAsync();
            return View(model);
        }
    }
}
