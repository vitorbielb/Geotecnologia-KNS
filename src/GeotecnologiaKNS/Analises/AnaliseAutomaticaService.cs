using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;
using GeotecnologiaKNS.Geo.Services;

namespace GeotecnologiaKNS.Analises;

public interface IAnaliseAutomaticaService
{
    /// <summary>
    /// Coloca a análise de uma solicitação na fila e devolve imediatamente.
    /// </summary>
    Task<AnaliseAutomatica> EnfileirarAsync(int solicitacaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa uma análise já enfileirada: cruza o perímetro contra as camadas
    /// ativas, aplica a política e grava o laudo.
    /// </summary>
    Task<AnaliseAutomatica> ProcessarAsync(int analiseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toma para si a próxima análise elegível, marcando-a como Processando.
    /// Devolve null quando não há trabalho.
    /// </summary>
    Task<int?> TomarProximaAsync(CancellationToken cancellationToken = default);
}

public class AnaliseAutomaticaService : IAnaliseAutomaticaService
{
    /// <summary>Tentativas antes de desistir e exigir tratamento humano.</summary>
    private const int MaximoDeTentativas = 3;

    private readonly ApplicationDbContext _context;
    private readonly IIntersecaoService _intersecao;
    private readonly IMotorDeRegras _motor;
    private readonly IPoliticaAnaliseRepository _politicas;
    private readonly IMedidorDeUso _medidor;
    private readonly ILogger<AnaliseAutomaticaService> _logger;

    public AnaliseAutomaticaService(
        ApplicationDbContext context,
        IIntersecaoService intersecao,
        IMotorDeRegras motor,
        IPoliticaAnaliseRepository politicas,
        IMedidorDeUso medidor,
        ILogger<AnaliseAutomaticaService> logger)
    {
        _context = context;
        _intersecao = intersecao;
        _motor = motor;
        _politicas = politicas;
        _medidor = medidor;
        _logger = logger;
    }

    public async Task<AnaliseAutomatica> EnfileirarAsync(int solicitacaoId, CancellationToken cancellationToken = default)
    {
        var solicitacao = await _context.Solicitacao
            .Include(x => x.Propriedade)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId, cancellationToken)
            ?? throw new InvalidOperationException($"Solicitação {solicitacaoId} não encontrada.");

        var propriedade = solicitacao.Propriedade
            ?? throw new InvalidOperationException($"Solicitação {solicitacaoId} sem propriedade vinculada.");

        var analise = new AnaliseAutomatica
        {
            TenantId = solicitacao.TenantId,
            SolicitacaoId = solicitacao.Id,
            CodigoCar = propriedade.CodigoCar,
            Situacao = SituacaoAnalise.Pendente
        };

        // A própria linha é a fila: gravada como Pendente, ela sobrevive a
        // reinício do servidor. Uma fila em memória perderia o trabalho de quem
        // acabou de enviar uma solicitação, sem deixar rastro.
        _context.AnalisesAutomaticas.Add(analise);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Análise {AnaliseId} enfileirada para a solicitação {SolicitacaoId}.",
            analise.Id, solicitacaoId);

        return analise;
    }

    public async Task<AnaliseAutomatica> ProcessarAsync(int analiseId, CancellationToken cancellationToken = default)
    {
        var analise = await _context.AnalisesAutomaticas
            .FirstOrDefaultAsync(x => x.Id == analiseId, cancellationToken)
            ?? throw new InvalidOperationException($"Análise {analiseId} não encontrada.");

        var solicitacao = await _context.Solicitacao
            .Include(x => x.Propriedade)
            .FirstOrDefaultAsync(x => x.Id == analise.SolicitacaoId, cancellationToken)
            ?? throw new InvalidOperationException($"Solicitação {analise.SolicitacaoId} não encontrada.");

        var propriedade = solicitacao.Propriedade
            ?? throw new InvalidOperationException($"Solicitação {analise.SolicitacaoId} sem propriedade vinculada.");

        // Cada indústria tem o próprio corte de conformidade; sem política
        // cadastrada, vale o protocolo padrão. Lida aqui, e não ao enfileirar,
        // porque o que o laudo precisa explicar é a regra que de fato rodou.
        var politica = await _politicas.ObterDoTenantAsync(solicitacao.TenantId, cancellationToken);

        analise.Politica = politica.Nome;

        // Retrato das regras vigentes no momento da análise: a política pode
        // ser afrouxada depois, e o laudo precisa continuar explicando o
        // veredito que deu na época.
        analise.PoliticaAplicada = RetratoDaPolitica.Serializar(politica);

        try
        {
            if (string.IsNullOrWhiteSpace(propriedade.CodigoCar))
            {
                throw new InvalidOperationException(
                    "A propriedade não tem número do CAR; a análise automática depende dele.");
            }

            var cruzamento = await _intersecao.CruzarPorCarAsync(propriedade.CodigoCar, cancellationToken);
            var avaliacao = _motor.Avaliar(cruzamento, politica);

            analise.AreaImovelHa = cruzamento.AreaImovelHa;
            analise.Resultado = avaliacao.Status;
            analise.Parecer = avaliacao.Parecer;
            analise.CamadasVerificadas = await DescreverCamadasAsync(cancellationToken);
            analise.CoberturaCompleta = avaliacao.CoberturaCompleta;
            analise.RegrasNaoAvaliadas = avaliacao.NaoAvaliadas.Count == 0
                ? null
                : string.Join(Environment.NewLine, avaliacao.NaoAvaliadas.Select(
                    r => $"{r.CodigoRegra} — {r.Descricao}"));
            analise.Situacao = SituacaoAnalise.Concluida;
            analise.ConcluidaEm = DateTime.Now;

            analise.Ocorrencias = avaliacao.Achados.Select(a => new AnaliseOcorrencia
            {
                CodigoRegra = a.CodigoRegra,
                Descricao = a.Descricao,
                Severidade = (int)a.Severidade,
                Camada = a.CamadaNome,
                Origem = a.Origem,
                Rotulo = a.Rotulo,
                AreaSobrepostaHa = a.AreaSobrepostaHa,
                PercentualDoImovel = a.PercentualDoImovel,
                Fundamento = a.Fundamento
            }).ToList();

            // O veredito da análise vira o status da solicitação. O analista
            // continua podendo sobrescrever pela tela de análise — a automação
            // decide o caso comum, não tira a palavra final de quem responde.
            solicitacao.Status = avaliacao.Status;

            // Resumo, não o laudo: a coluna da solicitação tem 2000 caracteres
            // e o laudo completo, que não tem limite, fica em analise.Parecer.
            solicitacao.Parecer = avaliacao.Resumo;
            solicitacao.DataAnalise = DateTime.Now;
            solicitacao.Analista = "Análise automática";

            await _context.SaveChangesAsync(cancellationToken);

            // Só o que chegou ao fim é cobrável: análise que falhou não entregou
            // resultado nenhum e seria indefensável numa fatura contestada.
            await _medidor.RegistrarAsync(
                analise.TenantId, TipoDeUso.AnaliseExecutada, analise.Id,
                propriedade.CodigoCar, cancellationToken);

            _logger.LogInformation(
                "Análise {AnaliseId} da solicitação {SolicitacaoId}: {Resultado} com {Ocorrencias} ocorrência(s).",
                analise.Id, analise.SolicitacaoId, avaliacao.Status, avaliacao.Achados.Count);

            if (!avaliacao.CoberturaCompleta)
            {
                _logger.LogWarning(
                    "Análise {AnaliseId} saiu com cobertura parcial: {Quantidade} regra(s) sem camada ({Regras}). " +
                    "Carregue as camadas correspondentes e reprocesse.",
                    analise.Id,
                    avaliacao.NaoAvaliadas.Count,
                    string.Join(", ", avaliacao.NaoAvaliadas.Select(r => r.CodigoRegra)));
            }
        }
        catch (Exception ex)
        {
            // A falha fica registrada e a solicitação não é liberada por omissão:
            // continua como Solicitado, à espera de tratamento humano.
            analise.Erro = ex.Message;

            if (analise.Tentativas < MaximoDeTentativas)
            {
                // Espera crescente: banco geoespacial reiniciando ou rede
                // instável se resolvem sozinhos em segundos, e insistir sem
                // pausa só multiplicaria o erro no log.
                analise.Situacao = SituacaoAnalise.Pendente;
                analise.ProximaTentativaEm =
                    DateTime.UtcNow.AddSeconds(EsperaEmSegundos(analise.Tentativas));

                _logger.LogWarning(ex,
                    "Análise {AnaliseId} falhou na tentativa {Tentativa} de {Maximo}; " +
                    "nova tentativa às {Momento}.",
                    analise.Id, analise.Tentativas, MaximoDeTentativas, analise.ProximaTentativaEm);
            }
            else
            {
                analise.Situacao = SituacaoAnalise.Falhou;
                analise.ConcluidaEm = DateTime.Now;

                _logger.LogError(ex,
                    "Análise {AnaliseId} da solicitação {SolicitacaoId} falhou em definitivo " +
                    "após {Tentativas} tentativas.",
                    analise.Id, analise.SolicitacaoId, analise.Tentativas);
            }

            await _context.SaveChangesAsync(CancellationToken.None);
        }

        return analise;
    }

    /// <summary>Espera antes da próxima tentativa: 15s, 60s, 240s.</summary>
    private static int EsperaEmSegundos(int tentativa) =>
        15 * (int)Math.Pow(4, Math.Max(0, tentativa - 1));

    public async Task<int?> TomarProximaAsync(CancellationToken cancellationToken = default)
    {
        // Um único UPDATE seleciona e marca: entre ler "está pendente" e
        // escrever "agora é minha" não existe intervalo em que outro processo
        // possa pegar a mesma análise. Fazer isso em dois passos no C# abriria
        // a janela para dois trabalhadores cruzarem o mesmo imóvel e gravarem
        // laudos duplicados.
        //
        // READPAST pula linhas que outra transação já travou, em vez de esperar
        // por elas — é o que permite mais de um trabalhador sem enfileirar um
        // atrás do outro.
        const string sql = @"
            UPDATE TOP (1) AnalisesAutomaticas WITH (ROWLOCK, READPAST)
               SET Situacao = @processando,
                   Tentativas = Tentativas + 1,
                   ProximaTentativaEm = NULL
            OUTPUT INSERTED.Id
             WHERE Situacao = @pendente
               AND (ProximaTentativaEm IS NULL OR ProximaTentativaEm <= @agora);";

        // Tipo e valor declarados separadamente de propósito. SituacaoAnalise.
        // Pendente vale zero, e new SqlParameter(nome, 0) casa com a sobrecarga
        // (string, SqlDbType) em vez da de valor — o parâmetro nasceria sem
        // valor e tipado como bigint. O banco recusa com "parameter not
        // supplied", e a fila para sem nunca processar nada.
        var ids = await _context.Database
            .SqlQueryRaw<int>(sql,
                new SqlParameter("@processando", SqlDbType.Int)
                    { Value = (int)SituacaoAnalise.Processando },
                new SqlParameter("@pendente", SqlDbType.Int)
                    { Value = (int)SituacaoAnalise.Pendente },
                new SqlParameter("@agora", SqlDbType.DateTime2)
                    { Value = DateTime.UtcNow })
            .ToListAsync(cancellationToken);

        return ids.Count > 0 ? ids[0] : null;
    }

    private async Task<string> DescreverCamadasAsync(CancellationToken cancellationToken)
    {
        var camadas = await _intersecao.ObterCamadasAtivasAsync(cancellationToken);
        var texto = new StringBuilder();

        foreach (var camada in camadas)
        {
            texto.AppendLine(
                $"{camada.Nome} ({camada.Origem})" +
                (camada.AnoReferencia.HasValue ? $" — ano {camada.AnoReferencia}" : string.Empty) +
                $" — {camada.TotalFeicoes} feições, atualizada em " +
                (camada.AtualizadaEm?.ToString("dd/MM/yyyy") ?? "data não registrada"));
        }

        return texto.Length == 0 ? "Nenhuma camada ativa." : texto.ToString();
    }
}

