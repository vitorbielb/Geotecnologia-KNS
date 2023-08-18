using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
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

        // GET: Propriedades/Details/5
        public async Task<IActionResult> Details(int? id)
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

        // GET: Propriedades/Create
        public IActionResult Create()
        {
            FillProdutoresUnidadesFederativasViewBag();
            return View();
        }

        // POST: Propriedades/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Propriedade propriedade)
        {
            propriedade.Produtor = _context.Produtores.Find(propriedade.ProdutorId)!;

            if (ModelState.IsValid)
            {
                _context.Add(propriedade);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            
            FillProdutoresUnidadesFederativasViewBag();
            return View(propriedade);
        }

        // GET: Propriedades/Edit/5
        // GET: Propriedades/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            FillProdutoresUnidadesFederativasViewBag();

            ViewBag.VinculoId = id;

            if (id == null || _context.Propriedades == null)
            {
                return NotFound();
            }

            var propriedade = await _context.Propriedades
                .Include(p => p.Arquivos) // Incluindo a lista de arquivos associados à propriedade
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,NomePropriedade,TipoPropriedade,CicloProducao,Area,AreaUtil,Latitude,Longitude,OrigemCoordenadas,Bioma,UnidadeFederativa,Municipio,Industria,TipoCadastroRural,Matricula,CadastroAmbientalRural,LicencaAmbiental,Ccir,Incra,Outros")] Propriedade propriedade)
        {
            if (id != propriedade.Id)
            {
                return NotFound();
            }

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
                return RedirectToAction(nameof(Index));
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

        public ActionResult GetCitiesByUF(Estado uf)
        {
            return Json(UnidadesFederativasExtension.GetCities(uf));
        }

        private bool PropriedadeExists(int id)
        {

            return (_context.Propriedades?.Any(e => e.Id == id)).GetValueOrDefault();

        }
        public IActionResult UploadArquivos(IList<IFormFile> arquivos, int propriedadeId)
        {
            IFormFile imagemCarregada = arquivos.FirstOrDefault();

            if (imagemCarregada != null)
            {
                using MemoryStream ms = new MemoryStream();
                imagemCarregada.OpenReadStream().CopyTo(ms);

                Arquivo arqui = new Arquivo()
                {
                    Descricao = imagemCarregada.FileName,
                    Dados = ms.ToArray(),
                    ContentType = imagemCarregada.ContentType
                };

                _context.Arquivos.Add(arqui);
                _context.SaveChanges();

                // Capturar a variável propriedade 
                var propriedade = _context.Propriedades.Include(p => p.Arquivos).FirstOrDefault(p => p.Id == propriedadeId);
                if (propriedade != null)
                {
                    propriedade.Arquivos.Add(arqui);
                    _context.SaveChanges();
                }
            }

            // Redirecionar para a View 'Edit' com o ID da propriedade para atualização
            return RedirectToAction("Edit", "Propriedades", new { id = propriedadeId });
        }
        public IActionResult Visualizar(int id)
        {
            var arquivosBanco = _context.Arquivos.FirstOrDefault(a => a.Id == id);

            return File(arquivosBanco.Dados, arquivosBanco.ContentType);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirArquivo(int id, int propriedadeId)
        {
            var arquivo = await _context.Arquivos.FindAsync(id);
            if (arquivo != null)
            {
                _context.Arquivos.Remove(arquivo);
                await _context.SaveChangesAsync();
            }
            // Redireciona para a view Edit com o ID da propriedade
            return RedirectToAction("Edit", new { id = propriedadeId });
        }

        private void FillProdutoresUnidadesFederativasViewBag()
        {
            ViewBag.UnidadesFederativas = UnidadesFederativasExtension.GetUnidadesFederativas();
            ViewBag.Produtores = _context.Produtores
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
    }
}