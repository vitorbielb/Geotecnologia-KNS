using System.Text;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;

namespace GeotecnologiaKNS.Analises;

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
    string? Fundamento);

/// <summary>
/// Uma regra que não pôde ser aplicada porque nenhuma camada do tipo que ela
/// examina estava carregada.
/// </summary>
/// <remarks>
/// Não é a mesma coisa que uma regra que passou. Sem este registro, um imóvel
/// verificado contra uma única camada saía com o mesmo "LIBERADO" de um imóvel
/// verificado contra todas — e o laudo não dava como distinguir os dois.
/// </remarks>
public record RegraNaoAvaliada(
    string CodigoRegra,
    string Descricao,
    TipoCamada Tipo,
    Severidade SeveridadePrevista);

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

public interface IMotorDeRegras
{
    ResultadoAvaliacao Avaliar(
        ResultadoCruzamento cruzamento,
        PoliticaAnalise politica,
        ConsultaPorDocumento? documento = null);
}

public class MotorDeRegras : IMotorDeRegras
{
    public ResultadoAvaliacao Avaliar(
        ResultadoCruzamento cruzamento,
        PoliticaAnalise politica,
        ConsultaPorDocumento? documento = null)
    {
        ArgumentNullException.ThrowIfNull(cruzamento);
        ArgumentNullException.ThrowIfNull(politica);

        var achados = new List<Achado>();

        foreach (var sobreposicao in cruzamento.Sobreposicoes)
        {
            foreach (var regra in politica.Regras.Where(r => !r.EhPorDocumento && r.Satisfeita(sobreposicao)))
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

        // Ordena por gravidade e, dentro dela, pela área — o laudo precisa abrir
        // com o achado que decide o veredito.
        achados = achados
            .OrderByDescending(a => a.Severidade)
            .ThenByDescending(a => a.AreaSobrepostaHa)
            .ToList();

        var naoAvaliadas = LevantarNaoAvaliadas(cruzamento, politica, documento);
        var status = DeterminarStatus(achados, naoAvaliadas);

        return new ResultadoAvaliacao(
            status,
            achados,
            naoAvaliadas,
            MontarParecer(cruzamento, achados, naoAvaliadas, status, politica),
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
                    regra.Fundamento);
            }
        }
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
        ConsultaPorDocumento? documento)
    {
        var verificados = cruzamento.TiposVerificados.ToHashSet();
        var listas = documento?.TiposDisponiveis.ToHashSet() ?? new HashSet<TipoRestricao>();
        var temDocumento = !string.IsNullOrWhiteSpace(documento?.Documento);

        return politica.Regras
            .Where(r => r.EhPorDocumento
                ? !temDocumento || !listas.Contains(r.Restricao!.Value)
                : !verificados.Contains(r.Tipo))
            .Select(r => new RegraNaoAvaliada(r.Codigo, r.Descricao, r.Tipo, r.Severidade))
            .OrderByDescending(r => r.SeveridadePrevista)
            .ThenBy(r => r.CodigoRegra, StringComparer.Ordinal)
            .ToList();
    }

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
        IReadOnlyList<string> Rotulos);

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
                     .ToList());
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

        return naoAvaliadas.Count == 0 ? Status.Liberado : Status.Alerta;
    }

    private static string MontarParecer(
        ResultadoCruzamento cruzamento,
        IReadOnlyList<Achado> achados,
        IReadOnlyList<RegraNaoAvaliada> naoAvaliadas,
        Status status,
        PoliticaAnalise politica)
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

            // Achado por documento não tem área: escrever "0,00 ha (0,00% do
            // imóvel)" faria parecer restrição irrelevante, quando é o
            // contrário — ela não depende de o imóvel tocar coisa alguma.
            var porDocumento = grupo.AreaTotalHa == 0 && grupo.PercentualTotal == 0;

            if (porDocumento)
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
                var titulo = porDocumento ? "Atos" : "Feições";
                texto.AppendLine($"  {titulo}: {string.Join(", ", grupo.Rotulos)}{reticencias}");
            }

            if (!string.IsNullOrWhiteSpace(grupo.Fundamento))
            {
                texto.AppendLine($"  Fundamento: {grupo.Fundamento}");
            }
        }

        EscreverNaoAvaliadas(texto, naoAvaliadas);

        return texto.ToString();
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
            "Nenhuma camada dos tipos abaixo estava carregada no momento da análise. " +
            "Estas regras não foram aplicadas — não se pode concluir que o imóvel as atende.");

        foreach (var regra in naoAvaliadas)
        {
            texto.AppendLine();
            texto.AppendLine($"[NÃO AVALIADA] {regra.CodigoRegra} — {regra.Descricao}");
            texto.AppendLine($"  Tipo de camada ausente: {DescreverTipo(regra.Tipo)}");
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
