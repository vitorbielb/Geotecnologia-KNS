using GeotecnologiaKNS.Geo;
using GeotecnologiaKNS.Geo.Ingestao;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Analises;

/// <summary>
/// Baixa do SICAR os municípios que alguém pediu e não estavam carregados.
/// </summary>
/// <remarks>
/// Fecha a alça que já existia pela metade: quando uma consulta não encontrava
/// o imóvel, o sistema registrava a lacuna — e ficava registrada, esperando
/// alguém reparar. Agora a lacuna é o pedido de carga, e ela se atende sozinha.
///
/// Por município, e não por estado, porque é a granularidade da demanda: um
/// estado inteiro são centenas de milhares de imóveis para atender um pedido.
/// O município de um fornecedor sai em segundos.
///
/// O efeito prático para quem usa: cadastrar um imóvel de um município que o
/// sistema nunca viu falha uma vez, e passa a funcionar na hora seguinte sem
/// ninguém ser acionado.
/// </remarks>
public class PreenchedorDaBaseCar : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    /// <summary>Espera antes da primeira conferência, para não disputar com a subida.</summary>
    private static readonly TimeSpan Partida = TimeSpan.FromMinutes(7);

    /// <summary>
    /// Municípios por rodada.
    /// </summary>
    /// <remarks>
    /// Um teto baixo é de propósito: se uma indústria cadastrar fornecedores de
    /// cinquenta municípios de uma vez, atender todos de enfiada prenderia a
    /// conexão com o SICAR por muito tempo. Os mais pedidos vêm primeiro, e o
    /// resto entra na rodada seguinte.
    /// </remarks>
    private const int PorRodada = 10;

    private readonly IServiceScopeFactory _escopos;
    private readonly IConfiguration _configuracao;
    private readonly ILogger<PreenchedorDaBaseCar> _logger;

    public PreenchedorDaBaseCar(
        IServiceScopeFactory escopos,
        IConfiguration configuracao,
        ILogger<PreenchedorDaBaseCar> logger)
    {
        _escopos = escopos;
        _configuracao = configuracao;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuracao.GetValue("Camadas:PreencherBaseCar", true))
        {
            _logger.LogInformation("Preenchimento automático da base do CAR desligado por configuração.");
            return;
        }

        if (!await EsperarAsync(Partida, stoppingToken))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AtenderLacunasAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // O laço não pode morrer: se morrer, as lacunas voltam a ficar
                // paradas e nada indica isso.
                _logger.LogError(ex, "Erro no laço do preenchimento da base do CAR.");
            }

            if (!await EsperarAsync(Intervalo, stoppingToken))
            {
                break;
            }
        }
    }

    private async Task AtenderLacunasAsync(CancellationToken stoppingToken)
    {
        List<string> pendentes;

        using (var escopo = _escopos.CreateScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<GeoDbContext>();

            var cobertos = await contexto.Cobertura
                .AsNoTracking()
                .Select(x => x.CodigoIbge)
                .ToListAsync(stoppingToken);

            pendentes = await contexto.Lacunas
                .AsNoTracking()
                .Where(x => !cobertos.Contains(x.CodigoIbge))
                .GroupBy(x => x.CodigoIbge)

                // Mais pedido primeiro: a lacuna que mais gente esbarrou é a que
                // mais atrapalha.
                .OrderByDescending(x => x.Sum(l => l.Consultas))
                .Select(x => x.Key)
                .Take(PorRodada)
                .ToListAsync(stoppingToken);
        }

        if (pendentes.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "{Quantidade} município(s) pedido(s) e não carregado(s): {Codigos}",
            pendentes.Count, string.Join(", ", pendentes));

        foreach (var codigoIbge in pendentes)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            // Um escopo por município: a carga enche o rastreador do contexto, e
            // reaproveitá-lo faria o consumo crescer com o total baixado em vez
            // de com o maior município.
            using var escopo = _escopos.CreateScope();

            var resultado = await escopo.ServiceProvider
                .GetRequiredService<BaixadorBaseCar>()
                .BaixarMunicipioAsync(codigoIbge, stoppingToken);

            if (!resultado.Sucesso)
            {
                _logger.LogWarning(
                    "O município {Codigo} segue sem cobertura. Motivo: {Motivo}",
                    codigoIbge, resultado.Erro);
            }
        }
    }

    private static async Task<bool> EsperarAsync(TimeSpan tempo, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(tempo, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
