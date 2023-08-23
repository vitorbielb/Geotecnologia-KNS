using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize(Roles = Global.Roles.ApplicationAdmin)]
    public class IndustriasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public IndustriasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Industrias
        [Authorize(Policy = "tenant_read")]
        public async Task<IActionResult> Index()
        {
              return _context.Industrias != null ? 
                          View(await _context.Industrias.ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.Industrias'  is null.");
        }

        // GET: Industrias/Details/5
        [Authorize(Policy = "tenant_read")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Industrias == null)
            {
                return NotFound();
            }

            var industria = await _context.Industrias
                .FirstOrDefaultAsync(m => m.TenantId == id);
            if (industria == null)
            {
                return NotFound();
            }

            return View(industria);
        }

        // GET: Industrias/Create
        [Authorize(Policy = "tenant_create")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Industrias/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Policy = "tenant_create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenantId,Imagem,Nome,NomeResumido,RazaoSocial,Cnpj")] Industria industria)
        {
            if (ModelState.IsValid)
            {
                _context.Add(industria);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(industria);
        }

        // GET: Industrias/Edit/5
        [Authorize(Policy = "tenant_update")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Industrias == null)
            {
                return NotFound();
            }

            var industria = await _context.Industrias.FindAsync(id);
            if (industria == null)
            {
                return NotFound();
            }
            return View(industria);
        }

        // POST: Industrias/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Policy = "tenant_update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TenantId,Imagem,Nome,NomeResumido,RazaoSocial,Cnpj")] Industria industria)
        {
            if (id != industria.TenantId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(industria);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IndustriaExists(industria.TenantId))
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
            return View(industria);
        }

        // GET: Industrias/Delete/5
        [Authorize(Policy = "tenant_delete")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Industrias == null)
            {
                return NotFound();
            }

            var industria = await _context.Industrias
                .FirstOrDefaultAsync(m => m.TenantId == id);
            if (industria == null)
            {
                return NotFound();
            }

            return View(industria);
        }

        // POST: Industrias/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Policy = "tenant_delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Industrias == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Industrias'  is null.");
            }
            var industria = await _context.Industrias.FindAsync(id);
            if (industria != null)
            {
                _context.Industrias.Remove(industria);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool IndustriaExists(int id)
        {
          return (_context.Industrias?.Any(e => e.TenantId == id)).GetValueOrDefault();
        }
    }
}
