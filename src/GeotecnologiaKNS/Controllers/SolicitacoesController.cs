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
                .Include(z => z.Documentos)
                .Include(q => q.Propriedade.Cartografia.Arquivos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (solicitacao == null)
            {
                return NotFound();
            }
            if (solicitacao.Propriedade.Cartografia == null)
            {
                solicitacao.Propriedade.Cartografia = new Cartografia
                {
                    Arquivos = new List<CartografiaArquivo>() // Inicializa uma lista vazia
                };
            }
            solicitacao.Cartografia ??= new Cartografia();

            return View(solicitacao);
        }

        // GET: Solicitacoes/Create
        public IActionResult Create()
        {
            FillPropriedadesViewBag();
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
                solicitacao.DataSolicitacao = DateTime.Now;
                _context.Add(solicitacao);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            FillPropriedadesViewBag();
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
        private void FillPropriedadesViewBag()
        {
            ViewBag.Propriedades = _context.Propriedades.Where(propriedades => propriedades.Validacao == Validacao.Validado)
                .ToSelectListItems(
                    x => x.NomePropriedade,
                    x => x.Id,
                    options => options.Placeholder = "Selecione...");
        }
        [Authorize(Policy = "UserCanUpdateSolicitacoes")]
        public async Task<IActionResult> Analise()
        {
            var solicitacoes = _context.Solicitacao.Include(s => s.Propriedade)
                                                   .Include(s => s.Propriedade.Produtor);
            return View(await solicitacoes.ToListAsync());
        }

        // GET: Solicitacoes/Edit/5
        [Authorize(Policy = "UserCanUpdateSolicitacoes")]
        public async Task<IActionResult> Edit(int? id)
        {
            ViewBag.Status = ((Status[])Enum.GetValues(typeof(Status)))
                   .ToSelectListItems(
                       x => x.ToString(),
                       x => (int)x,
                       options => options.Placeholder = "Selecione...");

            if (id == null || _context.Solicitacao == null)
            {
                return NotFound();
            }

            var solicitacao = await _context.Solicitacao
                .Include(p => p.Documentos) // Incluindo a lista de arquivos associados à propriedade
                .FirstOrDefaultAsync(p => p.Id == id);
            if (solicitacao == null)
            {
                return NotFound();
            }
            ViewData["PropriedadeId"] = new SelectList(_context.Propriedades.Where(propriedades => propriedades.Validacao == Validacao.Validado), "Id", "NomePropriedade", solicitacao.PropriedadeId);
            return View(solicitacao);
        }

        // POST: Solicitacoes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Edit([Bind("Id,PropriedadeId,Analista,Solicitante,DataSolicitacao,DataAnalise,Observacao,Status,Parecer")] Solicitacao solicitacao)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(solicitacao);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SolicitacaoExists(solicitacao.Id))
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
            ViewData["PropriedadeId"] = new SelectList(_context.Propriedades.Where(propriedades => propriedades.Validacao == Validacao.Validado), "Id", "NomePropriedade", solicitacao.PropriedadeId);
            return View(solicitacao);
        }
        [HttpPost, ActionName("Upload")]
        public async Task<ActionResult> UploadAsync(AnaliseArquivoViewModel arquivo)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var solicitacao = await _context.Solicitacao
                                            .Include(x => x.Documentos)
                                            .FirstAsync(x => x.Id == arquivo.VinculoId);

            solicitacao.Documentos ??= new List<AnaliseArquivo>();

            solicitacao.Documentos.Add(arquivo.Model);
            _context.Solicitacao.Update(solicitacao);

            await _context.SaveChangesAsync();

            return View("_file-list-Analise", solicitacao);
        }

        [HttpPost, ActionName("DeleteFile")]
        public async Task<ActionResult> DeleteFileAsync(int id)
        {
            var arquivo = await _context.AnalisesArquivos.FindAsync(id);

            if (arquivo == null)
            {
                return Problem();
            }

            var solicitacao = await _context.Solicitacao
                                         .Include(x => x.Documentos)
                                         .FirstAsync(x => x.Documentos!.Contains(arquivo));

            _context.AnalisesArquivos.Remove(arquivo);
            await _context.SaveChangesAsync();

            return View("_file-list-Analise", solicitacao);
        }

        [HttpGet("Solicitacoes/ViewFile/{id}")]
        public async Task<ActionResult> ViewFileAsync(int id)
        {
            var arquivo = await _context.AnalisesArquivos.FindAsync(id);

            if (arquivo == null)
            {
                return Problem();
            }

            return File(arquivo.Dados, arquivo.ContentType);
        }
    }
}