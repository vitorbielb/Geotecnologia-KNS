using System.Text;
using GeotecnologiaKNS.Geo.Services;

namespace GeotecnologiaKNS.Analises;

public interface IAnaliseAutomaticaService
{
    /// <summary>
    /// Executa a análise de uma solicitação: cruza o perímetro contra as camadas
    /// ativas, aplica a política e grava o laudo.
    /// </summary>
    Task<AnaliseAutomatica> ExecutarAsync(int solicitacaoId, CancellationToken cancellationToken = default);
}

public class AnaliseAutomaticaService : IAnaliseAutomaticaService
{
    private readonly ApplicationDbContext _context;
    private readonly IIntersecaoService _intersecao;
    private readonly IMotorDeRegras _motor;
    private readonly IPoliticaAnaliseRepository _politicas;
    private readonly ILogger<AnaliseAutomaticaService> _logger;

    public AnaliseAutomaticaService(
        ApplicationDbContext context,
        IIntersecaoService intersecao,
        IMotorDeRegras motor,
        IPoliticaAnaliseRepository politicas,
        ILogger<AnaliseAutomaticaService> logger)
    {
        _context = context;
        _intersecao = intersecao;
        _motor = motor;
        _politicas = politicas;
        _logger = logger;
    }

    public async Task<AnaliseAutomatica> ExecutarAsync(int solicitacaoId, CancellationToken cancellationToken = default)
    {
        var solicitacao = await _context.Solicitacao
            .Include(x => x.Propriedade)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId, cancellationToken)
            ?? throw new InvalidOperationException($"Solicitação {solicitacaoId} não encontrada.");

        var propriedade = solicitacao.Propriedade
            ?? throw new InvalidOperationException($"Solicitação {solicitacaoId} sem propriedade vinculada.");

        // Cada indústria tem o próprio corte de conformidade; sem política
        // cadastrada, vale o protocolo padrão.
        var politica = await _politicas.ObterDoTenantAsync(solicitacao.TenantId, cancellationToken);

        var analise = new AnaliseAutomatica
        {
            TenantId = solicitacao.TenantId,
            SolicitacaoId = solicitacao.Id,
            CodigoCar = propriedade.CodigoCar,
            Politica = politica.Nome,

            // Retrato das regras vigentes no momento da análise: a política pode
            // ser afrouxada depois, e o laudo precisa continuar explicando o
            // veredito que deu na época.
            PoliticaAplicada = RetratoDaPolitica.Serializar(politica)
        };

        _context.AnalisesAutomaticas.Add(analise);
        await _context.SaveChangesAsync(cancellationToken);

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

            _logger.LogInformation(
                "Análise {AnaliseId} da solicitação {SolicitacaoId}: {Resultado} com {Ocorrencias} ocorrência(s).",
                analise.Id, solicitacaoId, avaliacao.Status, avaliacao.Achados.Count);

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
            analise.Situacao = SituacaoAnalise.Falhou;
            analise.Erro = ex.Message;
            analise.ConcluidaEm = DateTime.Now;

            await _context.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex, "Falha na análise automática da solicitação {SolicitacaoId}.", solicitacaoId);
        }

        return analise;
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
