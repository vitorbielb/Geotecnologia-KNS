using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize(Roles = nameof(Roles.ApplicationAdmin))]
    public class ValidadesPropriedadeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ValidadesPropriedadeController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> IndexValidacaoPropriedade()
        {
            var model = await _context.Propriedades.Include(x => x.Produtor).ToListAsync();
            return View(model);
        }
    }
}