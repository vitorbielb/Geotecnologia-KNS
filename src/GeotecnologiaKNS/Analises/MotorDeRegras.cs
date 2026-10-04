using System.Text;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;

namespace GeotecnologiaKNS.Analises;

/// <summary>
/// Sobre o que o achado recai.
/// </summary>
/// <remarks>
/// O laudo escreve cada um de um jeito, e a diferença não é cosmética: dizer
/// "sobreposição de 0,00 ha" sobre um embargo em nome do produtor faria a
/// restrição parecer irrelevante, e dizer "em nome do produtor" sobre um achado
/// na cadeia indireta apontaria a pessoa errada.
/// </remarks>
public enum EscopoDoAchado
{
    /// <summary>Sobreposição geográfica no imóvel analisado.</summary>
    Imovel = 0,

    /// <summary>Restrição em nome do produtor, por CPF/CNPJ.</summary>
    Documento = 1,

    /// <summary>Restrição em imóvel que forneceu ao fornecedor direto.</summary>
    CadeiaIndireta = 2
}

/// <summary>
/// Uma regra que disparou, com a evidência que a fez disparar.
/// </summary>
public record Achado(
    string CodigoRegra,
    string Descricao,
    Severidade Severidade,
    string CamadaNome,
    string Origem,
    string? Rotulo,
    double AreaSobrepostaHa,
    double PercentualDoImovel,
    string? Fundamento,
    EscopoDoAchado Escopo = EscopoDoAchado.Imovel);

/// <summary>
/// Uma regra que não pôde ser aplicada porque nenhuma camada do tipo que ela
/// examina estava carregada.
/// </summary>
/// <remarks>
/// Não é a mesma coisa que uma regra que passou. Sem este registro, um imóvel
/// verificado contra uma única camada saía com o mesmo "LIBERADO" de um imóvel
/// verificado contra todas — e o laudo não dava como distinguir os dois.
/// </remarks>
/// <summary>
/// Por que uma regra ficou sem ser aplicada.
/// </summary>
/// <remarks>
/// Importa dizer qual é: "camada não carregada" manda carregar um arquivo, e
/// é a instrução errada para a IND-001, que fica sem avaliar porque ninguém
/// declarou a cadeia de fornecedores. Quem lê o laudo precisa saber o que fazer
/// para resolver, e as duas coisas se resolvem em lugares diferentes.
/// </remarks>
public enum MotivoNaoAvaliada
{
    CamadaAusente = 0,
    ListaRestritivaAusente = 1,
    ProdutorSemDocumento = 2,
    CadeiaNaoInformada = 3
}

public record RegraNaoAvaliada(
    string CodigoRegra,
    string Descricao,
    TipoCamada Tipo,
    Severidade SeveridadePrevista,
    MotivoNaoAvaliada Motivo = MotivoNaoAvaliada.CamadaAusente)
{
    /// <summary>O que resolve a falta, em uma frase.</summary>
    public string Explicacao => Motivo switch
    {
        MotivoNaoAvaliada.CadeiaNaoInformada =>
            "nenhum fornecedor indireto declarado para o imóvel",

        MotivoNaoAvaliada.ProdutorSemDocumento =>
            "o produtor está cadastrado sem CPF ou CNPJ",

        MotivoNaoAvaliada.ListaRestritivaAusente =>
            "a lista restritiva correspondente não está carregada",

        _ => "nenhuma camada do tipo examinado está carregada"
    };

    /// <summary>
    /// Indica se a falta desta regra muda o veredito.
    /// </summary>
    /// <remarks>
    /// Regra informativa, se não avaliada, continua só informando: não pode
    /// decidir mais calada do que decidiria falando.
    /// </remarks>
    public bool AlteraOVeredito => SeveridadePrevista != Severidade.Informativo;
}

public record ResultadoAvaliacao(
    Status Status,
    IReadOnlyList<Achado> Achados,
    IReadOnlyList<RegraNaoAvaliada> NaoAvaliadas,
    string Parecer,
    string Resumo)
{
    public bool TemBloqueio => Achados.Any(a => a.Severidade == Severidade.Bloqueio);
    public bool TemAlerta => Achados.Any(a => a.Severidade == Severidade.Alerta);

    /// <summary>Todas as regras da política puderam ser aplicadas.</summary>
    public bool CoberturaCompleta => NaoAvaliadas.Count == 0;
}

/// <summary>
/// O que a consulta às listas restritivas devolveu para um produtor.
/// </summary>
/// <param name="Documento">CPF ou CNPJ consultado, ou null se não havia.</param>
/// <param name="TiposDisponiveis">
/// Listas que estavam carregadas. Sem isto, "nada encontrado" ficaria
/// indistinguível de "não havia onde procurar" — o mesmo cuidado que vale para
/// as camadas.
/// </param>
public record ConsultaPorDocumento(
    string? Documento,
    IReadOnlyList<AchadoPorDocumento> Achados,
    IReadOnlyList<TipoRestricao> TiposDisponiveis);

/// <summary>
/// O que a verificação de um fornecedor indireto devolveu.
/// </summary>
/// <param name="Verificado">
/// Falso quando o imóvel não pôde ser conferido — fora da base do CAR, por
/// exemplo. Não é o mesmo que estar limpo, e o laudo não pode confundir os dois.
/// </param>
public record FornecedorIndiretoAvaliado(
    string CodigoCar,
    string? NomeProdutor,
    bool Verificado,
    IReadOnlyList<string> Restricoes,
    string? Observacao)
{
    public bool TemRestricao => Restricoes.Count > 0;
}

/// <summary>
/// A cadeia de fornecedores indiretos declarada para o imóvel.
/// </summary>
/// <param name="Declarados">
/// Quantos foram informados. Zero significa cadeia não informada — não
/// significa que não existe. A fazenda que vende boi gordo quase sempre comprou
/// bezerro de alguém.
/// </param>
public record CadeiaIndireta(
    int Declarados,
    IReadOnlyList<FornecedorIndiretoAvaliado> Avaliados)
{
    public bool Informada => Declarados > 0;

    public int NaoVerificados => Avaliados.Count(a => !a.Verificado);
}

public interface IMotorDeRegras
{
    ResultadoAvaliacao Avaliar(
        ResultadoCruzamento cruzamento,
        PoliticaAnalise politica,
        ConsultaPorDocumento? documento = null,
        CadeiaIndireta? cadeia = null);
}

public class MotorDeRegras : IMotorDeRegras
{
    public ResultadoAvaliacao Avaliar(
        ResultadoCruzamento cruzamento,
        PoliticaAnalise politica,
        ConsultaPorDocumento? documento = null,
        CadeiaIndireta? cadeia = null)
    {
        ArgumentNullException.ThrowIfNull(cruzamento);
        ArgumentNullException.ThrowIfNull(politica);

        var achados = new List<Achado>();

        foreach (var sobreposicao in cruzamento.Sobreposicoes)
        {
            foreach (var regra in politica.Regras.Where(r => r.EhGeografica && r.Satisfeita(sobreposicao)))
            {
                achados.Add(new Achado(
                    regra.Codigo,
                    regra.Descricao,
                    regra.Severidade,
                    sobreposicao.CamadaNome,
                    sobreposicao.Origem,
                    sobreposicao.Rotulo,
                    sobreposicao.AreaSobrepostaHa,
                    sobreposicao.PercentualDoImovel,
                    regra.Fundamento));
            }
        }

        achados.AddRange(AvaliarPorDocumento(politica, documento));
        achados.AddRange(AvaliarCadeiaIndireta(politica, cadeia));

        // Ordena por gravidade e, dentro dela, pela área — o laudo precisa abrir
        // com o achado que decide o veredito.
        achados = achados
            .OrderByDescending(a => a.Severidade)
            .ThenByDescending(a => a.AreaSobrepostaHa)
            .ToList();

        var naoAvaliadas = LevantarNaoAvaliadas(cruzamento, politica, documento, cadeia);
        var status = DeterminarStatus(achados, naoAvaliadas);

        return new ResultadoAvaliacao(
            status,
            achados,
            naoAvaliadas,
            MontarParecer(cruzamento, achados, naoAvaliadas, status, politica, cadeia),
            MontarResumo(achados, naoAvaliadas, status, politica.Regras.Count));
    }

    /// <summary>
    /// Produz um achado para cada restrição encontrada em nome do produtor.
    /// </summary>
    /// <remarks>
    /// Cada registro vira um achado próprio, como acontece com as
    /// sobreposições: o agrupamento por regra no laudo já junta o que for do
    /// mesmo tipo, e manter um por um preserva o número do ato para quem
    /// precisar conferir na origem.
    /// </remarks>
    private static IEnumerable<Achado> AvaliarPorDocumento(
        PoliticaAnalise politica, ConsultaPorDocumento? documento)
    {
        if (documento is null || documento.Achados.Count == 0)
        {
            yield break;
        }

        foreach (var regra in politica.Regras.Where(r => r.EhPorDocumento))
        {
            foreach (var achado in documento.Achados.Where(a => a.Tipo == regra.Restricao))
            {
                // Área e percentual ficam em zero: a restrição recai sobre a
                // pessoa, não sobre um pedaço do imóvel. O laudo trata esse
                // caso à parte justamente por isso.
                yield return new Achado(
                    regra.Codigo,
                    regra.Descricao,
                    regra.Severidade,
                    achado.Origem,
                    achado.Origem,
                    DescreverAchado(achado),
                    AreaSobrepostaHa: 0,
                    PercentualDoImovel: 0,
                    regra.Fundamento,
                    EscopoDoAchado.Documento);
            }
        }
    }

    /// <summary>
    /// Produz um achado por fornecedor indireto com restrição.
    /// </summary>
    /// <remarks>
    /// Um achado por fornecedor, e não um só para a cadeia inteira: quem lê o
    /// laudo precisa saber qual imóvel da cadeia tem o problema para ir atrás
    /// dele. Juntar tudo numa linha obrigaria a abrir outro relatório.
    /// </remarks>
    private static IEnumerable<Achado> AvaliarCadeiaIndireta(
        PoliticaAnalise politica, CadeiaIndireta? cadeia)
    {
        if (cadeia is null)
        {
            yield break;
        }

        foreach (var regra in politica.Regras.Where(r => r.CadeiaIndireta))
        {
            foreach (var fornecedor in cadeia.Avaliados.Where(a => a.TemRestricao))
            {
                yield return new Achado(
                    regra.Codigo,
                    regra.Descricao,
                    regra.Severidade,
                    "Cadeia de fornecimento indireto",
                    "Declarado pela indústria",
                    DescreverFornecedor(fornecedor),

                    // Zero, como nas regras por documento: a restrição é de
                    // outro imóvel, e exibir área daria a entender que é deste.
                    AreaSobrepostaHa: 0,
                    PercentualDoImovel: 0,
                    regra.Fundamento,
                    EscopoDoAchado.CadeiaIndireta);
            }
        }
    }

    /// <summary>
    /// Identifica o fornecedor na ocorrência, sem repetir o que a seção da
    /// cadeia já detalha.
    /// </summary>
    /// <remarks>
    /// Curto de propósito: listar aqui as vinte sobreposições de um fornecedor
    /// enchia o rótulo da ocorrência e estourava a coluna, derrubando a gravação
    /// da análise inteira. O detalhe mora na seção CADEIA DE FORNECIMENTO
    /// INDIRETO, que não tem limite de tamanho.
    /// </remarks>
    private static string DescreverFornecedor(FornecedorIndiretoAvaliado fornecedor)
    {
        var texto = new StringBuilder(fornecedor.CodigoCar);

        if (!string.IsNullOrWhiteSpace(fornecedor.NomeProdutor))
        {
            texto.Append(" — ").Append(fornecedor.NomeProdutor);
        }

        if (fornecedor.Restricoes.Count > 0)
        {
            texto.Append($" ({fornecedor.Restricoes.Count} restrição(ões))");
        }

        return texto.ToString();
    }

    private static string DescreverAchado(AchadoPorDocumento achado)
    {
        var texto = new StringBuilder();

        texto.Append(string.IsNullOrWhiteSpace(achado.Referencia)
            ? "sem referência"
            : achado.Referencia);

        if (!string.IsNullOrWhiteSpace(achado.NomeTitular))
        {
            texto.Append(" — ").Append(achado.NomeTitular);
        }

        if (!string.IsNullOrWhiteSpace(achado.Municipio))
        {
            texto.Append(" — ").Append(achado.Municipio);

            if (!string.IsNullOrWhiteSpace(achado.Uf))
            {
                texto.Append('/').Append(achado.Uf);
            }
        }

        // Marca o que já apareceu no cruzamento, para o laudo não dar a
        // impressão de duas restrições onde há uma.
        if (achado.TemGeometria)
        {
            texto.Append(" (com área delimitada)");
        }

        return texto.ToString();
    }

    /// <summary>
    /// Regras sem base para consulta: camada não carregada, ou lista restritiva
    /// ausente, ou produtor sem documento informado.
    /// </summary>
    private static List<RegraNaoAvaliada> LevantarNaoAvaliadas(
        ResultadoCruzamento cruzamento,
        PoliticaAnalise politica,
        ConsultaPorDocumento? documento,
        CadeiaIndireta? cadeia)
    {
        var verificados = cruzamento.TiposVerificados.ToHashSet();
        var listas = documento?.TiposDisponiveis.ToHashSet() ?? new HashSet<TipoRestricao>();
        var temDocumento = !string.IsNullOrWhiteSpace(documento?.Documento);

        // Cadeia não informada é regra não avaliada, e não regra cumprida. É o
        // ponto inteiro do recurso: a fazenda que vende boi gordo quase sempre
        // comprou bezerro de alguém, e dar o laudo por completo sem saber de
        // quem é afirmar o que não se apurou.
        var cadeiaInformada = cadeia?.Informada == true;

        return politica.Regras
            .Where(r => r.CadeiaIndireta
                ? !cadeiaInformada
                : r.EhPorDocumento
                    ? !temDocumento || !listas.Contains(r.Restricao!.Value)
                    : !verificados.Contains(r.Tipo))
            .Select(r => new RegraNaoAvaliada(
                r.Codigo, r.Descricao, r.Tipo, r.Severidade, MotivoDe(r, temDocumento)))
            .OrderByDescending(r => r.SeveridadePrevista)
            .ThenBy(r => r.CodigoRegra, StringComparer.Ordinal)
            .ToList();
    }

    private static MotivoNaoAvaliada MotivoDe(RegraAnalise regra, bool temDocumento) => regra switch
    {
        { CadeiaIndireta: true } => MotivoNaoAvaliada.CadeiaNaoInformada,
        { EhPorDocumento: true } when !temDocumento => MotivoNaoAvaliada.ProdutorSemDocumento,
        { EhPorDocumento: true } => MotivoNaoAvaliada.ListaRestritivaAusente,
        _ => MotivoNaoAvaliada.CamadaAusente
    };

    /// <summary>
    /// Resumo curto do veredito, para o campo Parecer da solicitação.
    /// </summary>
    /// <remarks>
    /// O laudo completo fica em AnaliseAutomatica.Parecer, que não tem limite de
    /// tamanho. A coluna da solicitação tem 2000 caracteres e um imóvel com
    /// dezenas de sobreposições estourava esse limite, derrubando a gravação.
    /// </remarks>
    private static string MontarResumo(
        IReadOnlyList<Achado> achados,
        IReadOnlyList<RegraNaoAvaliada> naoAvaliadas,
        Status status,
        int totalRegras)
    {
        var texto = new StringBuilder();
        texto.Append($"Análise automática: {status.ToString().ToUpperInvariant()}. ");

        if (achados.Count == 0)
        {
            texto.Append(naoAvaliadas.Count == 0
                ? "Nenhuma sobreposição restritiva nas camadas verificadas."
                : "Nenhuma sobreposição nas camadas disponíveis, mas a verificação está incompleta.");
        }
        else
        {
            texto.Append(string.Join("; ", AgruparPorRegra(achados).Select(g =>
                $"{g.CodigoRegra} {g.CamadaNome} — {Formatar(g.AreaTotalHa)} ha ({Formatar(g.PercentualTotal)}%)")));
            texto.Append('.');
        }

        // Vai no resumo, e não só no laudo, porque é este texto que aparece na
        // lista de solicitações — onde alguém decide se precisa abrir o caso.
        if (naoAvaliadas.Count > 0)
        {
            texto.Append($" COBERTURA PARCIAL: {naoAvaliadas.Count} de {totalRegras} regra(s) " +
                         "sem base para consulta (");
            texto.Append(string.Join(", ", naoAvaliadas.Take(6).Select(r => r.CodigoRegra)));
            texto.Append(naoAvaliadas.Count > 6 ? ", ...)." : ").");
        }

        texto.Append(" Laudo completo na análise.");

        var resumo = texto.ToString();
        return resumo.Length <= 2000 ? resumo : resumo[..1997] + "...";
    }

    private record GrupoAchado(
        string CodigoRegra,
        string Descricao,
        Severidade Severidade,
        string CamadaNome,
        string Origem,
        int Quantidade,
        double AreaTotalHa,
        double PercentualTotal,
        string? Fundamento,
        IReadOnlyList<string> Rotulos,
        EscopoDoAchado Escopo);

    /// <summary>
    /// Agrupa por regra e camada. Um imóvel pode tocar dezenas de polígonos da
    /// mesma camada, e listar um a um torna o laudo ilegível sem acrescentar
    /// informação — o que decide é o total sobreposto.
    /// </summary>
    private static List<GrupoAchado> AgruparPorRegra(IReadOnlyList<Achado> achados)
    {
        return achados
            .GroupBy(a => new { a.CodigoRegra, a.CamadaNome })
            .Select(g =>
            {
                var primeiro = g.First();
                return new GrupoAchado(
                    g.Key.CodigoRegra,
                    primeiro.Descricao,
                    g.Max(a => a.Severidade),
                    g.Key.CamadaNome,
                    primeiro.Origem,
                    g.Count(),
                    g.Sum(a => a.AreaSobrepostaHa),
                    g.Sum(a => a.PercentualDoImovel),
                    primeiro.Fundamento,
                    g.Where(a => !string.IsNullOrWhiteSpace(a.Rotulo))
                     .Select(a => a.Rotulo!)
                     .Distinct()
                     .Take(5)
                     .ToList(),
                    primeiro.Escopo);
            })
            .OrderByDescending(g => g.Severidade)
            .ThenByDescending(g => g.AreaTotalHa)
            .ToList();
    }

    /// <summary>
    /// Um único bloqueio bloqueia. O resto é alerta. Só libera quem passou por
    /// todas as regras da política.
    /// </summary>
    /// <remarks>
    /// A última condição é o ponto: liberar um imóvel que não foi confrontado
    /// com embargo, terra indígena ou unidade de conservação seria afirmar algo
    /// que a análise não apurou. Enquanto faltar base para uma regra, o caso vai
    /// para verificação humana em vez de sair aprovado por omissão.
    /// </remarks>
    private static Status DeterminarStatus(
        IReadOnlyList<Achado> achados,
        IReadOnlyList<RegraNaoAvaliada> naoAvaliadas)
    {
        if (achados.Any(a => a.Severidade == Severidade.Bloqueio))
        {
            return Status.Bloqueado;
        }

        if (achados.Any(a => a.Severidade == Severidade.Alerta))
        {
            return Status.Alerta;
        }

        // Regra que só informaria, se não avaliada, também só informa. Rebaixar
        // o veredito por causa dela diria que a análise está incompleta quando
        // a própria indústria declarou que aquele ponto não decide compra.
        var faltamRegrasQueDecidem = naoAvaliadas.Any(r => r.SeveridadePrevista != Severidade.Informativo);

        return faltamRegrasQueDecidem ? Status.Alerta : Status.Liberado;
    }

    private static string MontarParecer(
        ResultadoCruzamento cruzamento,
        IReadOnlyList<Achado> achados,
        IReadOnlyList<RegraNaoAvaliada> naoAvaliadas,
        Status status,
        PoliticaAnalise politica,
        CadeiaIndireta? cadeia)
    {
        var texto = new StringBuilder();

        texto.AppendLine($"Análise automática — {politica.Nome}");
        texto.AppendLine($"CAR: {cruzamento.CodigoCar}");
        texto.AppendLine($"Área do imóvel: {Formatar(cruzamento.AreaImovelHa)} ha");
        texto.AppendLine($"Executada em: {cruzamento.ExecutadoEm:dd/MM/yyyy HH:mm} (UTC)");
        texto.AppendLine(
            $"Abrangência: {politica.Regras.Count - naoAvaliadas.Count} de {politica.Regras.Count} " +
            "regra(s) da política puderam ser aplicadas.");
        texto.AppendLine();

        texto.AppendLine($"Resultado: {status.ToString().ToUpperInvariant()}");

        if (achados.Count == 0)
        {
            texto.AppendLine(naoAvaliadas.Count == 0
                ? "Nenhuma sobreposição restritiva encontrada nas camadas verificadas."
                : "Nenhuma sobreposição encontrada nas camadas disponíveis. O resultado não é " +
                  "uma liberação: parte das regras não pôde ser verificada, como detalhado abaixo.");

            EscreverCadeiaIndireta(texto, cadeia);
            EscreverNaoAvaliadas(texto, naoAvaliadas);
            return texto.ToString();
        }

        var grupos = AgruparPorRegra(achados);

        texto.AppendLine();
        texto.AppendLine($"Ocorrências: {grupos.Count} regra(s) acionada(s), {achados.Count} sobreposição(ões).");

        foreach (var grupo in grupos)
        {
            texto.AppendLine();
            texto.AppendLine($"[{grupo.Severidade.ToString().ToUpperInvariant()}] {grupo.CodigoRegra} — {grupo.Descricao}");

            // Achado sem área não é achado irrelevante: escrever "0,00 ha
            // (0,00% do imóvel)" faria parecer que é, quando é o contrário —
            // ele não depende de o imóvel tocar coisa alguma.
            if (grupo.Escopo == EscopoDoAchado.CadeiaIndireta)
            {
                texto.AppendLine($"  Fonte: {grupo.Origem}");
                texto.AppendLine(
                    $"  Fornecedores indiretos com restrição: {grupo.Quantidade}. " +
                    "O achado é na cadeia, não no imóvel analisado.");
            }
            else if (grupo.Escopo == EscopoDoAchado.Documento)
            {
                texto.AppendLine($"  Fonte: {grupo.Origem}");
                texto.AppendLine(
                    $"  Registros em nome do produtor: {grupo.Quantidade}. " +
                    "Independe da localização do imóvel.");
            }
            else
            {
                texto.AppendLine($"  Camada: {grupo.CamadaNome} ({grupo.Origem})");
                texto.AppendLine(
                    $"  Sobreposição: {Formatar(grupo.AreaTotalHa)} ha " +
                    $"({Formatar(grupo.PercentualTotal)}% do imóvel) em {grupo.Quantidade} polígono(s)");
            }

            if (grupo.Rotulos.Count > 0)
            {
                var reticencias = grupo.Quantidade > grupo.Rotulos.Count ? ", ..." : string.Empty;
                var titulo = grupo.Escopo switch
                {
                    EscopoDoAchado.CadeiaIndireta => "Imóveis",
                    EscopoDoAchado.Documento => "Atos",
                    _ => "Feições"
                };
                texto.AppendLine($"  {titulo}: {string.Join(", ", grupo.Rotulos)}{reticencias}");
            }

            if (!string.IsNullOrWhiteSpace(grupo.Fundamento))
            {
                texto.AppendLine($"  Fundamento: {grupo.Fundamento}");
            }
        }

        EscreverCadeiaIndireta(texto, cadeia);
        EscreverNaoAvaliadas(texto, naoAvaliadas);

        return texto.ToString();
    }

    /// <summary>
    /// Seção que declara o alcance real da verificação na cadeia indireta.
    /// </summary>
    /// <remarks>
    /// É a parte mais importante do recurso, e a mais fácil de escrever errado.
    /// O sistema só conhece os fornecedores que alguém declarou; se a fazenda
    /// comprou bezerro de dez e declarou três, as outras sete não existem para
    /// ele. Um laudo que diga "cadeia indireta verificada" sem essa ressalva
    /// vira documento de defesa com base falsa — e é justamente num
    /// questionamento do Ministério Público que isso apareceria.
    /// </remarks>
    private static void EscreverCadeiaIndireta(StringBuilder texto, CadeiaIndireta? cadeia)
    {
        texto.AppendLine();
        texto.AppendLine("CADEIA DE FORNECIMENTO INDIRETO");

        if (cadeia is null || !cadeia.Informada)
        {
            texto.AppendLine(
                "Nenhum fornecedor indireto foi declarado para este imóvel. A cadeia indireta " +
                "não foi verificada — o que não significa que não exista.");
            return;
        }

        var comRestricao = cadeia.Avaliados.Count(a => a.TemRestricao);

        texto.AppendLine(
            $"Declarados: {cadeia.Declarados}. Verificados: " +
            $"{cadeia.Avaliados.Count(a => a.Verificado)}. Com restrição: {comRestricao}.");

        texto.AppendLine(
            "A verificação alcança apenas os fornecedores informados. Fornecedores não " +
            "declarados não foram verificados e não estão refletidos neste resultado.");

        foreach (var fornecedor in cadeia.Avaliados.OrderByDescending(a => a.TemRestricao))
        {
            texto.AppendLine();

            var marca = !fornecedor.Verificado
                ? "NÃO VERIFICADO"
                : fornecedor.TemRestricao ? "COM RESTRIÇÃO" : "sem restrição";

            texto.Append($"  [{marca}] {fornecedor.CodigoCar}");

            if (!string.IsNullOrWhiteSpace(fornecedor.NomeProdutor))
            {
                texto.Append(" — ").Append(fornecedor.NomeProdutor);
            }

            texto.AppendLine();

            foreach (var restricao in fornecedor.Restricoes)
            {
                texto.AppendLine($"    {restricao}");
            }

            if (!string.IsNullOrWhiteSpace(fornecedor.Observacao))
            {
                texto.AppendLine($"    {fornecedor.Observacao}");
            }
        }
    }

    /// <summary>
    /// Seção do laudo que nomeia o que ficou de fora e por quê.
    /// </summary>
    /// <remarks>
    /// Escrita em todo laudo incompleto, inclusive nos que não acharam nada —
    /// é justamente ali que a omissão enganaria, porque um laudo sem ocorrências
    /// se lê como aprovação.
    /// </remarks>
    private static void EscreverNaoAvaliadas(StringBuilder texto, IReadOnlyList<RegraNaoAvaliada> naoAvaliadas)
    {
        if (naoAvaliadas.Count == 0)
        {
            return;
        }

        texto.AppendLine();
        texto.AppendLine("REGRAS NÃO AVALIADAS");
        texto.AppendLine(
            "As regras abaixo não puderam ser aplicadas por falta de base para consultá-las. " +
            "Elas não foram avaliadas — não se pode concluir que o imóvel as atende.");

        foreach (var regra in naoAvaliadas)
        {
            texto.AppendLine();
            texto.AppendLine($"[NÃO AVALIADA] {regra.CodigoRegra} — {regra.Descricao}");

            texto.AppendLine(regra.Motivo == MotivoNaoAvaliada.CamadaAusente
                ? $"  Motivo: {regra.Explicacao} ({DescreverTipo(regra.Tipo)})."
                : $"  Motivo: {regra.Explicacao}.");
            texto.AppendLine(
                $"  Severidade que teria sido aplicada: {regra.SeveridadePrevista.ToString().ToUpperInvariant()}");
        }
    }

    private static string DescreverTipo(TipoCamada tipo) => tipo switch
    {
        TipoCamada.EmbargoAmbiental => "embargo ambiental",
        TipoCamada.TerraIndigena => "terra indígena",
        TipoCamada.UnidadeConservacao => "unidade de conservação",
        TipoCamada.TerritorioQuilombola => "território quilombola",
        TipoCamada.AssentamentoRural => "assentamento rural",
        TipoCamada.DesmatamentoConsolidado => "desmatamento consolidado",
        TipoCamada.AlertaDesmatamento => "alerta de desmatamento",
        TipoCamada.Bioma => "limite de bioma",
        TipoCamada.OutroPerimetro => "outro perímetro restritivo definido pela indústria",
        _ => tipo.ToString()
    };

    private static string Formatar(double valor) =>
        valor.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
