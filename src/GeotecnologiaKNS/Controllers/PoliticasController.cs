using GeotecnologiaKNS.Analises;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    /// <summary>
    /// Regras de conformidade da indústria.
    /// </summary>
    /// <remarks>
    /// Quem altera os cortes decide o que bloqueia uma compra, então a tela fica
    /// atrás da mesma permissão da administração de usuários.
    /// </remarks>
    [Authorize(Policy = "UserCanUserCreate")]
    public class PoliticasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPoliticaAnaliseRepository _politicas;
        private readonly IUserContext _userContext;

        public PoliticasController(
            ApplicationDbContext context,
            IPoliticaAnaliseRepository politicas,
            IUserContext userContext)
        {
            _context = context;
            _politicas = politicas;
            _userContext = userContext;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            // O protocolo ganha regras com o tempo, e quem personalizou a
            // política ficava sem elas para sempre. Aqui é onde a indústria pode
            // revisá-las, então é aqui que elas entram.
            if (_userContext.TenantId is { } tenantId && tenantId > 0)
            {
                var novas = await _politicas.SincronizarComProtocoloAsync(tenantId, cancellationToken);

                if (novas > 0)
                {
                    ViewBag.RegrasNovas = novas;
                }
            }

            var politica = await _context.Politicas
                .Include(x => x.Regras)
                .FirstOrDefaultAsync(cancellationToken);

            if (politica is not null)
            {
                ViewBag.Personalizada = true;
                politica.Regras = politica.Regras.OrderBy(r => r.Codigo).ToList();
                return View(politica);
            }

            // Sem política própria, mostra o padrão em leitura, deixando claro
            // que é herdado e não editável até ser adotado.
            ViewBag.Personalizada = false;
            return View(ComoPoliticaTenant(PoliticaAnalise.Padrao()));
        }

        /// <summary>
        /// Materializa o protocolo padrão como política própria da indústria,
        /// a partir da qual ela passa a poder editar.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdotarPadrao(CancellationToken cancellationToken)
        {
            var tenantId = _userContext.TenantId;

            if (!tenantId.HasValue || tenantId <= 0)
            {
                return Forbid();
            }

            await _politicas.CriarAPartirDoPadraoAsync(tenantId.Value, cancellationToken);

            TempData["Sucesso"] = "Política criada a partir do protocolo padrão. As regras já podem ser ajustadas.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Salvar(List<RegraTenant> regras, CancellationToken cancellationToken)
        {
            var politica = await _context.Politicas
                .Include(x => x.Regras)
                .FirstOrDefaultAsync(cancellationToken);

            if (politica is null)
            {
                return NotFound();
            }

            var enviadas = (regras ?? new List<RegraTenant>()).ToDictionary(r => r.Id, r => r);

            // Atualiza apenas as regras que já pertencem a esta política: o Id
            // vem do formulário e não pode ser usado para alcançar outra.
            foreach (var regra in politica.Regras)
            {
                if (!enviadas.TryGetValue(regra.Id, out var enviada))
                {
                    continue;
                }

                regra.Severidade = Math.Clamp(enviada.Severidade, 0, 2);
                regra.AreaMinimaHa = Math.Max(0, enviada.AreaMinimaHa);
                regra.PercentualMinimo = Math.Clamp(enviada.PercentualMinimo, 0, 100);
                regra.AnoMinimo = enviada.AnoMinimo;
                regra.Ativa = enviada.Ativa;
            }

            politica.AtualizadaEm = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            TempData["Sucesso"] = "Regras atualizadas. Elas valem para as próximas análises; laudos já emitidos mantêm as regras da época.";
            return RedirectToAction(nameof(Index));
        }

        private static PoliticaTenant ComoPoliticaTenant(PoliticaAnalise padrao) => new()
        {
            Nome = padrao.Nome,
            Regras = padrao.Regras.Select(r => new RegraTenant
            {
                Codigo = r.Codigo,
                Descricao = r.Descricao,
                Tipo = r.Tipo,
                Severidade = (int)r.Severidade,
                AreaMinimaHa = r.AreaMinimaHa,
                PercentualMinimo = r.PercentualMinimo,
                AnoMinimo = r.AnoMinimo,
                Fundamento = r.Fundamento,
                Ativa = true
            }).OrderBy(r => r.Codigo).ToList()
        };
    }
}
