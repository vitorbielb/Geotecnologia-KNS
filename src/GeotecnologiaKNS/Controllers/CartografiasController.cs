using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize]
    public class CartografiasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartografiasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Solicitacoes
        [Authorize(Policy = "UserCanUpdateSolicitacoes")]
        public async Task<IActionResult> Index()
        {
            var solicitacoes = _context.Cartografias.Include(s => s.Propriedade);
            return View(await solicitacoes.ToListAsync());
        }

        // GET: Solicitacoes/Create
        [Authorize(Policy = "UserCanUpdateSolicitacoes")]
        public IActionResult Create()
        {
            FillPropriedadesViewBag();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Create([Bind("Id,PropriedadeId,Solicitacao,Tipo,DataCartografia")] Cartografia solicitacao)
        {
            if (ModelState.IsValid)
            {
                _context.Add(solicitacao);
                await _context.SaveChangesAsync();
                return RedirectToAction("Edit", new { id = solicitacao.Id });
            }
            FillPropriedadesViewBag();
            return View(solicitacao);
        }

        private bool SolicitacaoExists(int id)
        {
            return (_context.Cartografias?.Any(e => e.Id == id)).GetValueOrDefault();
        }
        private void FillPropriedadesViewBag()
        {
            var propriedadesUsadasEmCartografia = _context.Cartografias
                .Select(c => c.PropriedadeId)
                .Distinct();

            ViewBag.Propriedades = _context.Propriedades
                .Where(p => p.Validacao == Validacao.Validado && !propriedadesUsadasEmCartografia.Contains(p.Id))
                .Select(p => new SelectListItem
                {
                    Text = p.NomePropriedade,
                    Value = p.Id.ToString()
                })
                .ToList();

            ViewBag.Propriedades.Insert(0, new SelectListItem
            {
                Text = "Selecione...",
                Value = string.Empty
            });
        }

        [Authorize(Policy = "UserCanUpdateSolicitacoes")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Cartografias == null)
            {
                return NotFound();
            }

            var solicitacao = await _context.Cartografias
                .Include(p => p.Arquivos)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (solicitacao == null)
            {
                return NotFound();
            }
            ViewData["Propriedades"] = new SelectList(_context.Propriedades.Where(propriedades => propriedades.Validacao == Validacao.Validado), "Id", "NomePropriedade", solicitacao.PropriedadeId);
            return View(solicitacao);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Edit([Bind("Id,PropriedadeId,Solicitacao,DataCartografia")] Cartografia cartografia)
        {
            ModelState.Remove("Documentos");

            if (ModelState.IsValid)
            {
                _context.Cartografias.Update(cartografia);
                await _context.SaveChangesAsync();
                return RedirectToAction("Analise", "Produtores");
            }

            var persitedProdutor = await _context.Cartografias
                                         .Include(x => x.Arquivos)
                                         .FirstAsync(x => x.Id == cartografia.Id);

            cartografia.Arquivos = persitedProdutor.Arquivos;
            ViewData["PropriedadeId"] = new SelectList(_context.Propriedades.Where(propriedades => propriedades.Validacao == Validacao.Validado), "Id", "NomePropriedade", cartografia.PropriedadeId);
            return View(cartografia);
        }
        [HttpPost, ActionName("Upload")]
        public async Task<ActionResult> UploadAsync(CartografiaArquivoViewModel arquivo)
        {

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }
          
            var solicitacao = await _context.Cartografias
                                            .Include(x => x.Arquivos)
                                            .FirstAsync(x => x.Id == arquivo.VinculoId);

            solicitacao.Arquivos ??= new List<CartografiaArquivo>();

            solicitacao.Arquivos.Add(arquivo.Model);
            _context.Cartografias.Update(solicitacao);

            await _context.SaveChangesAsync();

            return View("_file-listCart", solicitacao);
        }

        [HttpPost, ActionName("DeleteFile")]
        public async Task<ActionResult> DeleteFileAsync(int id)
        {
            var cartografia = await _context.CartografiasArquivos.FindAsync(id);

            if (cartografia == null)
            {
                return Problem();
            }

            var solicitacao = await _context.Cartografias
                                         .Include(x => x.Arquivos)
                                         .FirstAsync(x => x.Arquivos!.Contains(cartografia));

            _context.CartografiasArquivos.Remove(cartografia);
            await _context.SaveChangesAsync();

            return View("_file-listCart", solicitacao);
        }

        [HttpGet("Cartografias/ViewFile/{id}")]
        public async Task<ActionResult> ViewFileAsync(int id)
        {
            var arquivo = await _context.CartografiasArquivos.FindAsync(id);

            if (arquivo == null)
            {
                return Problem();
            }

            return File(arquivo.Dados, arquivo.ContentType);
        }
    }
}