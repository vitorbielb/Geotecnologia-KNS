namespace GeotecnologiaKNS.Analises;

/// <summary>
/// Consome a fila de análises pendentes.
/// </summary>
/// <remarks>
/// A análise saiu de dentro da requisição. Ela cruza o perímetro do imóvel
/// contra as camadas no PostGIS, e isso cresce com a base: hoje são segundos,
/// com a base nacional do CAR e as oito camadas pode passar do tempo limite —
/// e o usuário receberia um erro de tela por causa de um trabalho que, na
/// verdade, rodou.
///
/// Dentro do próprio processo, e não num serviço separado, porque a fila mora
/// no banco: a linha da análise gravada como Pendente é a fila. Isso já dá
/// sobrevivência a reinício, que é o que um Hangfire da vida traria, sem
/// acrescentar dependência nem um segundo esquema para operar. Trocar por um
/// agendador externo depois não mexe na análise em si — só em quem chama
/// TomarProximaAsync.
/// </remarks>
public class ProcessadorDeAnalises : BackgroundService
{
    /// <summary>Pausa quando a fila está vazia.</summary>
    private static readonly TimeSpan Ocioso = TimeSpan.FromSeconds(5);

    /// <summary>Pausa após uma falha do próprio laço, para não girar em vão.</summary>
    private static readonly TimeSpan AposErro = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<ProcessadorDeAnalises> _logger;

    public ProcessadorDeAnalises(IServiceScopeFactory escopos, ILogger<ProcessadorDeAnalises> logger)
    {
        _escopos = escopos;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processador de análises iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            bool havia;

            try
            {
                havia = await ProcessarUmaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Falha aqui é do laço, não da análise — banco fora do ar, por
                // exemplo. O laço não pode morrer: se morrer, a fila para de
                // andar em silêncio e ninguém percebe até alguém reclamar.
                _logger.LogError(ex, "Erro no laço do processador de análises.");
                havia = false;

                await EsperarAsync(AposErro, stoppingToken);
                continue;
            }

            if (!havia)
            {
                await EsperarAsync(Ocioso, stoppingToken);
            }
        }

        _logger.LogInformation("Processador de análises encerrado.");
    }

    /// <summary>
    /// Processa uma análise. Devolve false quando não havia nada na fila.
    /// </summary>
    private async Task<bool> ProcessarUmaAsync(CancellationToken cancellationToken)
    {
        // Escopo próprio por item: o DbContext é por requisição, e reaproveitar
        // um só para a vida toda do serviço acumularia o rastreador de mudanças
        // e envelheceria os dados lidos.
        using var escopo = _escopos.CreateScope();
        var analises = escopo.ServiceProvider.GetRequiredService<IAnaliseAutomaticaService>();

        var id = await analises.TomarProximaAsync(cancellationToken);

        if (id is null)
        {
            return false;
        }

        // ProcessarAsync já trata a própria falha: registra o erro, decide entre
        // reagendar e desistir, e nunca deixa a solicitação liberada por omissão.
        await analises.ProcessarAsync(id.Value, cancellationToken);

        return true;
    }

    private static async Task EsperarAsync(TimeSpan intervalo, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(intervalo, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Desligamento durante a pausa é o caminho normal de encerramento.
        }
    }
}
