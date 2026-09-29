using GeotecnologiaKNS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize]
    public class PropriedadesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPropriedadeCarService _carService;
        private readonly IMedidorDeUso _medidor;

        public PropriedadesController(
            ApplicationDbContext context,
            IPropriedadeCarService carService,
            IMedidorDeUso medidor)
        {
            _medidor = medidor;
            _context = context;
            _carService = carService;
        }

        // GET: Propriedades
        public async Task<IActionResult> Index()
        {
            var model = await _context.Propriedades.Include(x => x.Produtor).ToListAsync();
            return View(model);

        }
        [Authorize(Policy = "UserCanUpdateSolicitacoes")]
        public async Task<IActionResult> Analise()
        {
            var model = await _context.Propriedades.Include(x => x.Produtor).ToListAsync();
            return View(model);
        }
        private ActionResult HttpNotFound()
        {
            return NotFound();
        }
        // GET: Propriedades/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            FillProdutoresViewBag();

            if (id == null || _context.Propriedades == null)
            {
                return NotFound();
            }

            var propriedade = await _context.Propriedades
                .Include(p => p.Documentos)
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
            FillProdutoresViewBag();
            return View();
        }

        /// <summary>
        /// Consulta o CAR na base pública e devolve o que será gravado, para que
        /// o usuário confira antes de salvar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ConsultarCar(string? codigoCar, CancellationToken cancellationToken)
        {
            var consulta = await _carService.ConsultarAsync(codigoCar, cancellationToken);

            if (!consulta.Sucesso)
            {
                return Ok(new { sucesso = false, mensagem = consulta.Mensagem });
            }

            var imovel = consulta.Imovel!;

            return Ok(new
            {
                sucesso = true,
                mensagem = consulta.Mensagem,
                codigoCar = imovel.CodigoCar,
                municipio = imovel.Municipio,
                uf = imovel.Uf,
                situacao = imovel.Situacao,
                areaHa = imovel.AreaHa,
                areaCalculadaHa = imovel.AreaCalculadaHa,
                centroLat = imovel.CentroLat,
                centroLng = imovel.CentroLng,
                origem = imovel.Origem,
                baseCarregadaEm = imovel.BaseCarregadaEm,
                perimetro = imovel.PerimetroGeoJson
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Create(Propriedade propriedade, CancellationToken cancellationToken)
        {
            // Campos derivados não vêm do formulário; não podem bloquear a validação.
            ModelState.Remove(nameof(Propriedade.Produtor));
            ModelState.Remove(nameof(Propriedade.Municipio));
            ModelState.Remove(nameof(Propriedade.Area));

            if (!await _context.Produtores.AnyAsync(x => x.Id == propriedade.ProdutorId, cancellationToken))
            {
                ModelState.AddModelError(nameof(Propriedade.ProdutorId), "Produtor não encontrado.");
            }

            var consulta = await _carService.ConsultarAsync(propriedade.CodigoCar, cancellationToken);

            if (!consulta.Sucesso)
            {
                ModelState.AddModelError(nameof(Propriedade.CodigoCar), consulta.Mensagem);
            }
            else if (await _context.Propriedades.AnyAsync(x => x.CodigoCar == consulta.Imovel!.CodigoCar, cancellationToken))
            {
                ModelState.AddModelError(nameof(Propriedade.CodigoCar), "Este CAR já está cadastrado.");
            }

            if (!ModelState.IsValid)
            {
                FillProdutoresViewBag();
                return View(propriedade);
            }

            _carService.Aplicar(propriedade, consulta.Imovel!);

            _context.Add(propriedade);
            await _context.SaveChangesAsync(cancellationToken);

            await _medidor.RegistrarAsync(
                propriedade.TenantId, TipoDeUso.ImovelCadastrado, propriedade.Id,
                propriedade.CodigoCar, cancellationToken);

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Recarrega os dados do imóvel a partir da versão corrente da base do CAR.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarPeloCar(int id, CancellationToken cancellationToken)
        {
            var propriedade = await _context.Propriedades.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (propriedade is null)
            {
                return NotFound();
            }

            var consulta = await _carService.ConsultarAsync(propriedade.CodigoCar, cancellationToken);

            if (!consulta.Sucesso)
            {
                TempData["Erro"] = consulta.Mensagem;
                return RedirectToAction(nameof(Details), new { id });
            }

            _carService.Aplicar(propriedade, consulta.Imovel!);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["Sucesso"] = "Dados atualizados a partir da base do CAR.";
            return RedirectToAction(nameof(Details), new { id });
        }
        [Authorize(Policy = "UserCanUpdateSolicitacoes")]
        public async Task<IActionResult> Edit(int? id)
        {
            ViewBag.Validacao = ((Validacao[])Enum.GetValues(typeof(Validacao)))
              .ToSelectListItems(
                  x => x.ToString(),
                  x => (int)x,
                  options => options.Placeholder = "Selecione...");
            FillProdutoresViewBag();

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
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantFilter]
        public async Task<IActionResult> Edit(Propriedade propriedade, CancellationToken cancellationToken)
        {
            ModelState.Remove(nameof(Propriedade.Produtor));
            ModelState.Remove(nameof(Propriedade.Municipio));
            ModelState.Remove(nameof(Propriedade.Area));
            ModelState.Remove(nameof(Propriedade.CodigoCar));

            if (!ModelState.IsValid)
            {
                FillProdutoresViewBag();
                return View(propriedade);
            }

            // Carrega e atualiza campo a campo. Um _context.Update com a entidade
            // vinda do formulário apagaria o perímetro e a procedência do CAR,
            // que não trafegam pelo form.
            var persistida = await _context.Propriedades
                .FirstOrDefaultAsync(x => x.Id == propriedade.Id, cancellationToken);

            if (persistida is null)
            {
                return NotFound();
            }

            // Nome em branco na edição significa "manter o que está lá", não apagar.
            if (!string.IsNullOrWhiteSpace(propriedade.NomePropriedade))
            {
                persistida.NomePropriedade = propriedade.NomePropriedade.Trim();
            }
            persistida.ProdutorId = propriedade.ProdutorId;
            persistida.TipoPropriedade = propriedade.TipoPropriedade;
            persistida.CicloProducao = propriedade.CicloProducao;
            persistida.AreaUtil = propriedade.AreaUtil;
            persistida.TipoCadastroRural = propriedade.TipoCadastroRural;
            persistida.Validacao = propriedade.Validacao;

            await _context.SaveChangesAsync(cancellationToken);

            return RedirectToAction("Analise", "Propriedades");
        }

        // GET: Propriedades/Delete/5
        public ActionResult Delete(int id)
        {
            var propriedades = _context.Propriedades.Find(id);

            if (propriedades == null)
            {
                return HttpNotFound();
            }

            return View(propriedades);
        }

        // POST: Produtores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmedAsync(int id)
        {
            var propriedades = _context.Propriedades.Find(id);

            if (propriedades != null)
            {
                _context.Propriedades.Remove(propriedades);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        private void FillProdutoresViewBag()
        {
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

        [HttpPost, ActionName("Upload")]
        [ValidateAntiForgeryToken]
        // Sem o filtro, o documento era gravado com TenantId 0 e o filtro global
        // o escondia da própria indústria: subia, sumia da lista e ninguém
        // entendia por quê.
        [TenantFilter]
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
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteFileAsync(int id)
        {
            var arquivo = await _context.PropriedadesArquivos.FindAsync(id);

            if (arquivo == null)
            {
                // NotFound e não Problem: o documento pode simplesmente não existir, ou
                // pertencer a outra indústria e ser filtrado. Nenhum dos dois é erro
                // do servidor, e devolver 500 ainda poluiria o monitoramento.
                return NotFound();
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
                // NotFound e não Problem: o documento pode simplesmente não existir, ou
                // pertencer a outra indústria e ser filtrado. Nenhum dos dois é erro
                // do servidor, e devolver 500 ainda poluiria o monitoramento.
                return NotFound();
            }

            return File(arquivo.Dados, arquivo.ContentType);
        }
    }
}