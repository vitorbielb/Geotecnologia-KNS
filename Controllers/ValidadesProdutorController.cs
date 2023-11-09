using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize(Roles = nameof(Roles.ApplicationAdmin))]
    public class ValidadesProdutorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ValidadesProdutorController(ApplicationDbContext context)
        {
            _context = context;
        }

       public async Task<ActionResult> IndexAsync()
        {
            var produtores = await _context.Produtores.ToListAsync();
            return View(produtores);
        }

    }
}
