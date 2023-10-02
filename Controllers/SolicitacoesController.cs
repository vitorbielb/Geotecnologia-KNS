using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize]
    public class SolicitacoesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SolicitacoesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Solicitacoes
        public async Task<IActionResult> Index()
        {
            var solicitacoes = _context.Solicitacao.Include(s => s.Propriedade)
                                                   .Include(s => s.Propriedade.Produtor);
            return View(await solicitacoes.ToListAsync());
        }

        // GET: Solicitacoes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Solicitacao == null)
            {
                return NotFound();
            }

            var solicitacao = await _context.Solicitacao
                .Include(x => x.Propriedade.Produtor)
                .Include(s => s.Propriedade)
                .Include(p => p.Propriedade.Documentos)
                .Include(y => y.Propriedade.Produtor.Documentos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (solicitacao == null)
            {
                return NotFound();
            }

            return View(solicitacao);
        }

        // GET: Solicitacoes/Create
        public IActionResult Create()
        {
            ViewData["PropriedadeId"] = new SelectList(_context.Propriedades, "Id", "NomePropriedade");
            return View();
        }

        // POST: Solicitacoes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Create([Bind("Id,PropriedadeId,Analista,Solicitante,DataSolicitacao,DataAnalise,Observacao,Status")] Solicitacao solicitacao)
        {
            if (ModelState.IsValid)
            {
                _context.Add(solicitacao);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PropriedadeId"] = new SelectList(_context.Propriedades, "Id", "NomePropriedade", solicitacao.PropriedadeId);
            return View(solicitacao);
        }

        // GET: Solicitacoes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Solicitacao == null)
            {
                return NotFound();
            }

            var solicitacao = await _context.Solicitacao
                .Include(s => s.Propriedade)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (solicitacao == null)
            {
                return NotFound();
            }

            return View(solicitacao);
        }

        // POST: Solicitacoes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Solicitacao == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Solicitacao'  is null.");
            }
            var solicitacao = await _context.Solicitacao.FindAsync(id);
            if (solicitacao != null)
            {
                _context.Solicitacao.Remove(solicitacao);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SolicitacaoExists(int id)
        {
            return (_context.Solicitacao?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
