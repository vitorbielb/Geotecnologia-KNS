using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize]
    public class PropriedadesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PropriedadesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Propriedades
        public async Task<IActionResult> Index()
        {
            var model = await _context.Propriedades.Include(x => x.Produtor).ToListAsync();
            return View(model);

        }
        public async Task<IActionResult> IndexValidacao()
        {
            var model = await _context.Propriedades.Include(x => x.Produtor).ToListAsync();
            return View(model);
        }

        // GET: Propriedades/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            ViewBag.Validacao = ((Validacao[])Enum.GetValues(typeof(Situacao)))
              .ToSelectListItems(
                  x => x.ToString(),
                  (Func<Validacao, object>)(x => (int)x),
                  options => options.Placeholder = "Selecione...");
            FillProdutoresUnidadesFederativasViewBag();

            if (id == null || _context.Propriedades == null)
            {
                return NotFound();
            }

            var propriedade = await _context.Propriedades
                .Include(p => p.Documentos) // Incluindo a lista de arquivos associados à propriedade
                .FirstOrDefaultAsync(p => p.Id == id);

            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }

        // GET: Propriedades/Create
        public IActionResult Create()
        {
            FillProdutoresUnidadesFederativasViewBag();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Create(Propriedade propriedade)
        {
            propriedade.Produtor = _context.Produtores.Find(propriedade.Id)!;

            if (ModelState.IsValid)
            {
                _context.Add(propriedade);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            FillProdutoresUnidadesFederativasViewBag();
            return View(propriedade);
        }


        public async Task<IActionResult> Edit(int? id)
        {
            ViewBag.Validacao = ((Validacao[])Enum.GetValues(typeof(Situacao)))
              .ToSelectListItems(
                  x => x.ToString(),
                  (Func<Validacao, object>)(x => (int)x),
                  options => options.Placeholder = "Selecione...");
            FillProdutoresUnidadesFederativasViewBag();

            if (id == null || _context.Propriedades == null)
            {
                return NotFound();
            }

            var propriedade = await _context.Propriedades
                .Include(p => p.Documentos) // Incluindo a lista de arquivos associados à propriedade
                .FirstOrDefaultAsync(p => p.Id == id);

            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }


        // POST: Propriedades/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Edit([Bind("Id,NomePropriedade,TipoPropriedade,CicloProducao,Area,AreaUtil,Latitude,Longitude,OrigemCoordenadas,Bioma,UnidadeFederativa,Municipio,Industria,TipoCadastroRural,Matricula,CadastroAmbientalRural,LicencaAmbiental,Ccir,Incra,ProdutorId,Validacao,Outros")] Models.Propriedade propriedade)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(propriedade);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PropriedadeExists(propriedade.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction("Index", "ValidadesPropriedade");
            }
            return View(propriedade);
        }

        // GET: Propriedades/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {

            if (id == null || _context.Propriedades == null)

            {
                return NotFound();
            }


            var propriedade = await _context.Propriedades

                .FirstOrDefaultAsync(m => m.Id == id);
            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }

        // POST: Propriedades/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            if (_context.Propriedades == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Propriedade'  is null.");
            }
            var propriedade = await _context.Propriedades.FindAsync(id);
            if (propriedade != null)
            {
                _context.Propriedades.Remove(propriedade);

            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public ActionResult GetCitiesByUF(Estados uf)
        {
            return Json(UnidadesFederativasExtension.GetCities(uf));
        }

        private bool PropriedadeExists(int id)
        {

            return (_context.Propriedades?.Any(e => e.Id == id)).GetValueOrDefault();

        }

        private void FillProdutoresUnidadesFederativasViewBag()
        {
            ViewBag.UnidadesFederativas = UnidadesFederativasExtension.GetUnidadesFederativas();
            ViewBag.Produtores = _context.Produtores.Where(produtores => produtores.Situacao == Situacao.Validado)
                .ToSelectListItems(
                    x => x.Nome,
                    x => x.Id,
                    options => options.Placeholder = "Selecione...");
        }
        public async Task<IActionResult> Monitoramento()
        {
            var model = await _context.Propriedades.Include(x => x.Produtor).ToListAsync();
            return View(model);
        }

        [HttpPost, ActionName("Upload")]
        public async Task<ActionResult> UploadAsync(PropriedadeArquivoViewModel arquivo)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var propriedade = await _context.Propriedades
                                            .Include(x => x.Documentos)
                                            .FirstAsync(x => x.Id == arquivo.VinculoId);

            propriedade.Documentos ??= new List<PropriedadeArquivo>();

            propriedade.Documentos.Add(arquivo.Model);
            _context.Propriedades.Update(propriedade);

            await _context.SaveChangesAsync();

            return View("_file-list", propriedade);
        }

        [HttpPost, ActionName("DeleteFile")]
        public async Task<ActionResult> DeleteFileAsync(int id)
        {
            var arquivo = await _context.PropriedadesArquivos.FindAsync(id);

            if (arquivo == null)
            {
                return Problem();
            }

            var produtor = await _context.Propriedades
                                         .Include(x => x.Documentos)
                                         .FirstAsync(x => x.Documentos!.Contains(arquivo));

            _context.PropriedadesArquivos.Remove(arquivo);
            await _context.SaveChangesAsync();

            return View("_file-list", produtor);
        }

        [HttpGet("Propriedades/ViewFile/{id}")]
        public async Task<ActionResult> ViewFileAsync(int id)
        {
            var arquivo = await _context.PropriedadesArquivos.FindAsync(id);

            if (arquivo == null)
            {
                return Problem();
            }

            return File(arquivo.Dados, arquivo.ContentType);
        }
    }
}