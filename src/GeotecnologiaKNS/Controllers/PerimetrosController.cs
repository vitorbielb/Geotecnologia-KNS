using GeotecnologiaKNS.Geo.Ingestao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    /// <summary>
    /// Perímetros restritivos que a própria indústria define.
    /// </summary>
    /// <remarks>
    /// Fica atrás da mesma permissão das regras de conformidade, e pelo mesmo
    /// motivo: subir um perímetro aqui passa a bloquear compras, exatamente como
    /// mexer num corte da política.
    /// </remarks>
    [Authorize(Policy = "UserCanUserCreate")]
    public class PerimetrosController : Controller
    {
        /// <summary>
        /// Teto do arquivo enviado.
        /// </summary>
        /// <remarks>
        /// Perímetro próprio é uma lista de áreas conhecidas, não uma base
        /// nacional — quem precisa subir mais que isto está tentando usar a tela
        /// para outra coisa, e o caminho certo é uma camada pública no catálogo.
        /// </remarks>
        private const long TamanhoMaximo = 50L * 1024 * 1024;

        private readonly IPerimetroProprioService _perimetros;
        private readonly IUserContext _userContext;
        private readonly ILogger<PerimetrosController> _logger;

        public PerimetrosController(
            IPerimetroProprioService perimetros,
            IUserContext userContext,
            ILogger<PerimetrosController> logger)
        {
            _perimetros = perimetros;
            _userContext = userContext;
            _logger = logger;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            if (_userContext.TenantId is not { } tenantId || tenantId <= 0)
            {
                return View(Array.Empty<Geo.Entities.CamadaReferencia>());
            }

            return View(await _perimetros.ListarAsync(tenantId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(TamanhoMaximo)]
        public async Task<IActionResult> Enviar(
            string nome, IFormFile? arquivo, CancellationToken cancellationToken)
        {
            if (_userContext.TenantId is not { } tenantId || tenantId <= 0)
            {
                TempData["Erro"] = "Sua conta não está vinculada a uma indústria.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Dê um nome ao perímetro — é assim que ele aparece no laudo.";
                return RedirectToAction(nameof(Index));
            }

            if (arquivo is null || arquivo.Length == 0)
            {
                TempData["Erro"] = "Escolha um arquivo .zip (shapefile) ou .geojson.";
                return RedirectToAction(nameof(Index));
            }

            if (arquivo.Length > TamanhoMaximo)
            {
                TempData["Erro"] = $"O arquivo tem {arquivo.Length / 1024 / 1024} MB e o limite é 50 MB.";
                return RedirectToAction(nameof(Index));
            }

            // Gravado em disco antes de ler: os leitores de shapefile e GeoJSON
            // trabalham em fluxo sobre arquivo, e manter 50 MB em memória por
            // envio simultâneo derrubaria o servidor sem precisar de malícia.
            var temporario = Path.Combine(
                Path.GetTempPath(), $"envio-{Guid.NewGuid():N}{Path.GetExtension(arquivo.FileName)}");

            try
            {
                await using (var destino = System.IO.File.Create(temporario))
                {
                    await arquivo.CopyToAsync(destino, cancellationToken);
                }

                var resultado = await _perimetros.ImportarAsync(
                    temporario, nome.Trim(), tenantId, cancellationToken);

                TempData["Sucesso"] =
                    $"Perímetro \"{resultado.Nome}\" carregado com {resultado.Feicoes:N0} área(s)." +
                    (resultado.Descartados > 0
                        ? $" {resultado.Descartados:N0} foram descartadas por geometria inválida."
                        : string.Empty);
            }
            catch (Exception ex)
            {
                // A mensagem do serviço é escrita para quem opera, não para quem
                // depura: ela diz o que fazer com o arquivo.
                _logger.LogError(ex, "Falha ao carregar perímetro da indústria {TenantId}.", tenantId);
                TempData["Erro"] = ex.Message;
            }
            finally
            {
                Apagar(temporario);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
        {
            if (_userContext.TenantId is not { } tenantId || tenantId <= 0)
            {
                return Forbid();
            }

            var removido = await _perimetros.RemoverAsync(id, tenantId, cancellationToken);

            TempData[removido ? "Sucesso" : "Erro"] = removido
                ? "Perímetro removido."
                : "Perímetro não encontrado.";

            return RedirectToAction(nameof(Index));
        }

        private static void Apagar(string caminho)
        {
            try
            {
                if (System.IO.File.Exists(caminho))
                {
                    System.IO.File.Delete(caminho);
                }
            }
            catch (IOException)
            {
                // Temporário preso não desfaz uma importação que deu certo.
            }
        }
    }
}
