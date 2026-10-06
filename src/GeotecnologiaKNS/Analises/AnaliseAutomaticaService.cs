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
    private readonly IRestricaoDocumentoService _restricoes;
    private readonly IMedidorDeUso _medidor;
    private readonly ILogger<AnaliseAutomaticaService> _logger;

    public AnaliseAutomaticaService(
        ApplicationDbContext context,
        IIntersecaoService intersecao,
        IMotorDeRegras motor,
        IPoliticaAnaliseRepository politicas,
        IRestricaoDocumentoService restricoes,
        IMedidorDeUso medidor,
        ILogger<AnaliseAutomaticaService> logger)
    {
        _context = context;
        _intersecao = intersecao;
        _motor = motor;
        _politicas = politicas;
        _restricoes = restricoes;
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
            .Include(x => x.Propriedade!).ThenInclude(p => p.Produtor)
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

            // O tenant vai explícito porque a indústria pode ter perímetros
            // próprios, e o processamento roda em segundo plano, onde o filtro
            // global por inquilino está desligado de propósito.
            var cruzamento = await _intersecao.CruzarPorCarAsync(
                propriedade.CodigoCar, solicitacao.TenantId, cancellationToken);

            // O documento do produtor, não o do imóvel: a restrição recai sobre
            // a pessoa e acompanha quem ela é, não onde está a fazenda.
            var documento = await ConsultarDocumentoAsync(
                propriedade.Produtor?.Cpf, cancellationToken);

            // A fazenda que vende boi gordo quase sempre comprou bezerro de
            // outra, e é na outra que o passivo costuma estar. Olhar só o
            // fornecedor direto dá laudo limpo sobre cadeia que não é.
            var cadeia = await VerificarCadeiaIndiretaAsync(
                propriedade.Id, solicitacao.TenantId, cancellationToken);

            var avaliacao = _motor.Avaliar(cruzamento, politica, documento, cadeia);

            analise.AreaImovelHa = cruzamento.AreaImovelHa;
            analise.Resultado = avaliacao.Status;
            analise.Parecer = avaliacao.Parecer;
            analise.CamadasVerificadas = await DescreverCamadasAsync(
                cruzamento.CodigoCar, solicitacao.TenantId, cancellationToken);
            analise.CoberturaCompleta = avaliacao.CoberturaCompleta;
            // O motivo vai junto porque "camada não carregada" manda carregar
            // um arquivo, e essa é a instrução errada para a regra da cadeia
            // indireta, que se resolve declarando fornecedor. Quem lê precisa
            // saber onde agir.
            analise.RegrasNaoAvaliadas = avaliacao.NaoAvaliadas.Count == 0
                ? null
                : string.Join(Environment.NewLine, avaliacao.NaoAvaliadas.Select(
                    r => $"{r.CodigoRegra} — {r.Descricao} ({r.Explicacao})"));
            analise.Situacao = SituacaoAnalise.Concluida;
            analise.ConcluidaEm = DateTime.Now;

            // Os textos são cortados no tamanho da coluna porque vêm de fora:
            // nome de embargado, rótulo de feição, nome de fornecedor. Um deles
            // passar do limite derrubava a gravação da análise inteira — a
            // ocorrência mais longa fazia o laudo não existir.
            analise.Ocorrencias = avaliacao.Achados.Select(a => new AnaliseOcorrencia
            {
                CodigoRegra = Cortar(a.CodigoRegra, 20)!,
                Descricao = Cortar(a.Descricao, 300)!,
                Severidade = (int)a.Severidade,
                Camada = Cortar(a.CamadaNome, 200)!,
                Origem = Cortar(a.Origem, 200)!,
                Rotulo = Cortar(a.Rotulo, 300),
                AreaSobrepostaHa = a.AreaSobrepostaHa,
                PercentualDoImovel = a.PercentualDoImovel,
                Fundamento = Cortar(a.Fundamento, 500)
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

    /// <summary>
    /// Consulta as listas restritivas pelo documento do produtor.
    /// </summary>
    /// <remarks>
    /// Devolve o resultado mesmo quando não há documento ou nada é encontrado,
    /// porque o motor precisa distinguir "consultado e limpo" de "não havia o
    /// que consultar" — produtor sem CPF cadastrado não pode contar como
    /// verificado.
    /// </remarks>
    private async Task<ConsultaPorDocumento> ConsultarDocumentoAsync(
        string? documento, CancellationToken cancellationToken)
    {
        var tipos = await _restricoes.ObterTiposDisponiveisAsync(cancellationToken);
        var achados = await _restricoes.ConsultarAsync(documento, cancellationToken);

        return new ConsultaPorDocumento(
            RestricaoDocumentoService.Normalizar(documento), achados, tipos);
    }

    /// <summary>
    /// Verifica cada fornecedor indireto declarado para o imóvel.
    /// </summary>
    /// <remarks>
    /// Cada fornecedor custa um cruzamento geoespacial completo, então o
    /// trabalho cresce com o tamanho da cadeia. Vale a pena mesmo assim: é a
    /// única forma de o laudo falar da cadeia, e a análise já roda em segundo
    /// plano, fora da requisição.
    ///
    /// Falha em um fornecedor não derruba a análise do imóvel: ela vira "não
    /// verificado" com o motivo, que é informação melhor que nenhuma — e muito
    /// melhor que um laudo que não sai.
    /// </remarks>
    private async Task<CadeiaIndireta> VerificarCadeiaIndiretaAsync(
        int propriedadeId, int tenantId, CancellationToken cancellationToken)
    {
        // Teto por análise: uma cadeia declarada com centenas de imóveis
        // travaria a fila inteira atrás de uma solicitação só.
        const int MaximoPorAnalise = 50;

        var fornecedores = await _context.FornecedoresIndiretos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.PropriedadeId == propriedadeId && x.TenantId == tenantId)
            .OrderBy(x => x.Id)
            .Take(MaximoPorAnalise)
            .ToListAsync(cancellationToken);

        var declarados = await _context.FornecedoresIndiretos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(x => x.PropriedadeId == propriedadeId && x.TenantId == tenantId, cancellationToken);

        var avaliados = new List<FornecedorIndiretoAvaliado>(fornecedores.Count);

        foreach (var fornecedor in fornecedores)
        {
            avaliados.Add(await VerificarFornecedorAsync(fornecedor, tenantId, cancellationToken));
        }

        if (declarados > fornecedores.Count)
        {
            avaliados.Add(new FornecedorIndiretoAvaliado(
                $"(+{declarados - fornecedores.Count} não verificados)",
                null,
                Verificado: false,
                Array.Empty<string>(),
                $"A cadeia declarada passa de {MaximoPorAnalise} imóveis; os excedentes não " +
                "entraram nesta análise."));
        }

        return new CadeiaIndireta(declarados, avaliados);
    }

    private async Task<FornecedorIndiretoAvaliado> VerificarFornecedorAsync(
        FornecedorIndireto fornecedor, int tenantId, CancellationToken cancellationToken)
    {
        var restricoes = new List<string>();

        // O documento é verificado antes da geografia, e separadamente: ele
        // funciona mesmo quando o imóvel está fora da base do CAR, que é o caso
        // mais comum na ponta da cadeia.
        var porDocumento = await _restricoes.ConsultarAsync(fornecedor.Documento, cancellationToken);

        restricoes.AddRange(porDocumento.Select(
            a => $"{a.Origem}: {a.Referencia ?? "sem referência"}"));

        try
        {
            var cruzamento = await _intersecao.CruzarPorCarAsync(
                fornecedor.CodigoCar, tenantId, cancellationToken);

            restricoes.AddRange(cruzamento.Sobreposicoes.Select(
                s => $"{s.CamadaNome}: {Formatar(s.AreaSobrepostaHa)} ha " +
                     $"({Formatar(s.PercentualDoImovel)}% do imóvel)"));

            return new FornecedorIndiretoAvaliado(
                fornecedor.CodigoCar, fornecedor.NomeProdutor,
                Verificado: true, restricoes, Observacao: null);
        }
        catch (Exception ex)
        {
            // Imóvel fora da base do CAR é o caso esperado, não exceção rara.
            // Marcar como não verificado, e não como limpo: a diferença entre
            // os dois é o que o laudo existe para registrar.
            _logger.LogInformation(
                "Fornecedor indireto {Car} não pôde ser cruzado: {Motivo}",
                fornecedor.CodigoCar, ex.Message);

            return new FornecedorIndiretoAvaliado(
                fornecedor.CodigoCar, fornecedor.NomeProdutor,
                Verificado: false, restricoes,
                Observacao: $"Não foi possível cruzar o perímetro: {ex.Message}");
        }
    }

    /// <summary>Corta no tamanho da coluna, marcando que houve corte.</summary>
    private static string? Cortar(string? texto, int tamanho)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length <= tamanho)
        {
            return texto;
        }

        return texto[..(tamanho - 1)] + "…";
    }

    private static string Formatar(double valor) =>
        valor.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    /// <summary>
    /// A lista de bases que o laudo cita, separando o que respondeu do que não
    /// alcança este imóvel.
    /// </summary>
    /// <remarks>
    /// A separação é o ponto. Antes a lista era a mesma para o país inteiro, e
    /// o laudo de uma fazenda de Goiás trazia "PRODES — 48.750 feições" entre
    /// as bases consultadas — uma camada da Amazônia, sem um polígono sequer no
    /// estado, citada como se tivesse respondido.
    ///
    /// As que não alcançam continuam na lista, e dizendo que não alcançam.
    /// Sumir com elas trocaria uma informação errada por uma lacuna, e quem lê
    /// o laudo não teria como saber que a camada existe.
    /// </remarks>
    private async Task<string> DescreverCamadasAsync(
        string codigoCar, int tenantId, CancellationToken cancellationToken)
    {
        var camadas = await _intersecao.ObterCamadasAtivasAsync(
            codigoCar, tenantId, cancellationToken);

        if (camadas.Count == 0)
        {
            return "Nenhuma camada ativa.";
        }

        var texto = new StringBuilder();

        foreach (var consultada in camadas.Where(x => x.CobreOImovel))
        {
            var camada = consultada.Camada;

            texto.AppendLine(
                $"{camada.Nome} ({camada.Origem})" +
                (camada.AnoReferencia.HasValue ? $" — ano {camada.AnoReferencia}" : string.Empty) +
                (camada.CobreDesdeAno.HasValue ? $" — desde {camada.CobreDesdeAno}" : string.Empty) +
                $" — {camada.TotalFeicoes} feições, atualizada em " +
                (camada.AtualizadaEm?.ToString("dd/MM/yyyy") ?? "data não registrada"));
        }

        var foraDoAlcance = camadas.Where(x => !x.CobreOImovel).ToList();

        if (foraDoAlcance.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("Não alcançam este imóvel (região fora da cobertura da camada):");

            foreach (var consultada in foraDoAlcance)
            {
                texto.AppendLine($"{consultada.Camada.Nome} ({consultada.Camada.Origem})");
            }
        }

        return texto.ToString();
    }
}

