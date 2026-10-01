using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.Analises;

/// <summary>
/// Recarrega sozinho as camadas de referência que passaram do prazo.
/// </summary>
/// <remarks>
/// Sem isto, o sistema funciona hoje e vai apodrecendo: a base de embargos
/// continua sendo a do dia da instalação, e todo termo lavrado depois passa
/// despercebido. O laudo não ficaria em branco — ficaria dizendo "nenhuma
/// sobreposição encontrada", com a mesma confiança de sempre. É o pior formato
/// possível de erro, porque não se parece com erro nenhum.
///
/// Roda dentro da aplicação, e não num agendador do sistema operacional, pela
/// mesma razão que a fila de análises: é um componente a menos para instalar,
/// configurar e esquecer. Quem preferir o agendador externo tem o comando
/// <c>recarregar</c> na ferramenta de linha, que faz exatamente isto e devolve
/// código de saída diferente de zero quando alguma origem falha.
///
/// A segurança de deixar isto solto vem de três camadas que já existem antes
/// dele: a troca versionada, que nunca deixa a camada pela metade; a conferência
/// de <see cref="GuardaDeCarga"/>, que recusa arquivo truncado na origem; e a
/// trava do PostgreSQL, que impede duas recargas simultâneas da mesma camada.
/// </remarks>
public class AtualizadorDeCamadas : BackgroundService
{
    /// <summary>De quanto em quanto tempo o prazo das camadas é conferido.</summary>
    /// <remarks>
    /// Conferir é barato — uma consulta —, recarregar é que é caro, e só
    /// acontece quando a camada venceu de fato. De hora em hora o prazo mais
    /// curto do catálogo (sete dias) é respeitado com folga.
    /// </remarks>
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    /// <summary>
    /// Espera antes da primeira conferência, para não disputar com a subida.
    /// </summary>
    /// <remarks>
    /// Uma recarga começando junto com a aplicação atrasaria as primeiras
    /// requisições e, pior, rodaria de novo a cada reinício durante um deploy.
    /// </remarks>
    private static readonly TimeSpan Partida = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _escopos;
    private readonly IConfiguration _configuracao;
    private readonly ILogger<AtualizadorDeCamadas> _logger;

    public AtualizadorDeCamadas(
        IServiceScopeFactory escopos,
        IConfiguration configuracao,
        ILogger<AtualizadorDeCamadas> logger)
    {
        _escopos = escopos;
        _configuracao = configuracao;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Em desenvolvimento, baixar 200 MB do IBAMA a cada subida da aplicação
        // não ajuda ninguém. Fica desligado por padrão fora de produção, e
        // ligável por configuração para quem quiser testar.
        if (!_configuracao.GetValue("Camadas:RecargaAutomatica", true))
        {
            _logger.LogInformation("Recarga automática de camadas desligada por configuração.");
            return;
        }

        _logger.LogInformation("Atualizador de camadas iniciado.");

        if (!await EsperarAsync(Partida, stoppingToken))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConferirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // O laço não pode morrer: se morrer, as camadas param de ser
                // atualizadas sem nada indicar isso, que é exatamente a falha
                // silenciosa que este serviço existe para evitar.
                _logger.LogError(ex, "Erro no laço do atualizador de camadas.");
            }

            if (!await EsperarAsync(Intervalo, stoppingToken))
            {
                break;
            }
        }

        _logger.LogInformation("Atualizador de camadas encerrado.");
    }

    private async Task ConferirAsync(CancellationToken stoppingToken)
    {
        using var escopo = _escopos.CreateScope();

        var recarregador = escopo.ServiceProvider.GetRequiredService<RecarregadorDeCamadas>();

        await recarregador.SincronizarCatalogoAsync(stoppingToken);

        await AvisarListasVencidasAsync(recarregador, stoppingToken);

        var vencidas = await recarregador.ObterVencidasAsync(stoppingToken);

        if (vencidas.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "{Quantidade} camada(s) vencida(s): {Chaves}",
            vencidas.Count, string.Join(", ", vencidas.Select(x => x.Chave)));

        foreach (var fonte in vencidas)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            // Uma camada por escopo: as cargas grandes enchem o rastreador do
            // contexto, e reaproveitá-lo entre camadas faria o consumo crescer
            // com o total baixado em vez de com a maior camada.
            using var escopoDaCamada = _escopos.CreateScope();

            var resultado = await escopoDaCamada.ServiceProvider
                .GetRequiredService<RecarregadorDeCamadas>()
                .RecarregarAsync(fonte, cancellationToken: stoppingToken);

            if (!resultado.Sucesso)
            {
                // Já registrado com a exceção lá dentro; aqui o que interessa é
                // a leitura operacional: a camada continua no ar, velha, e vai
                // ser tentada de novo na próxima conferência.
                _logger.LogWarning(
                    "Camada {Chave} segue com a versão anterior. Motivo: {Motivo}",
                    fonte.Chave, resultado.Mensagem);
            }
        }
    }

    /// <summary>
    /// Reclama das listas restritivas que dependem de alguém e venceram.
    /// </summary>
    /// <remarks>
    /// O Cadastro de Empregadores do MTE vem em PDF e não é recarregado
    /// sozinho. Deixar isso implícito seria o pior dos mundos: a recarga
    /// automática daria a impressão de que tudo se mantém atualizado, e a lista
    /// de trabalho análogo a escravo envelheceria em silêncio enquanto a regra
    /// TRB-001 continua se declarando avaliada.
    /// </remarks>
    private async Task AvisarListasVencidasAsync(
        RecarregadorDeCamadas recarregador, CancellationToken stoppingToken)
    {
        foreach (var lista in await recarregador.ObterListasVencidasAsync(stoppingToken))
        {
            _logger.LogWarning(
                "A lista {Nome} está vencida desde {Vencimento:dd/MM/yyyy} e não tem recarga " +
                "automática. Siga docs/camadas-de-referencia.md para atualizá-la.",
                lista.Nome, lista.VenceEm);
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
