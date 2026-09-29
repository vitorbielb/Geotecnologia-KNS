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

public interface IMotorDeRegras
{
    ResultadoAvaliacao Avaliar(ResultadoCruzamento cruzamento, PoliticaAnalise politica);
}

public class MotorDeRegras : IMotorDeRegras
{
    public ResultadoAvaliacao Avaliar(ResultadoCruzamento cruzamento, PoliticaAnalise politica)
    {
        ArgumentNullException.ThrowIfNull(cruzamento);
        ArgumentNullException.ThrowIfNull(politica);

        var achados = new List<Achado>();

        foreach (var sobreposicao in cruzamento.Sobreposicoes)
        {
            foreach (var regra in politica.Regras.Where(r => r.Satisfeita(sobreposicao)))
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

        // Ordena por gravidade e, dentro dela, pela área — o laudo precisa abrir
        // com o achado que decide o veredito.
        achados = achados
            .OrderByDescending(a => a.Severidade)
            .ThenByDescending(a => a.AreaSobrepostaHa)
            .ToList();

        var naoAvaliadas = LevantarNaoAvaliadas(cruzamento, politica);
        var status = DeterminarStatus(achados, naoAvaliadas);

        return new ResultadoAvaliacao(
            status,
            achados,
            naoAvaliadas,
            MontarParecer(cruzamento, achados, naoAvaliadas, status, politica),
            MontarResumo(achados, naoAvaliadas, status, politica.Regras.Count));
    }

    /// <summary>
    /// Regras cujo tipo de camada não estava carregado no momento do cruzamento.
    /// </summary>
    private static List<RegraNaoAvaliada> LevantarNaoAvaliadas(
        ResultadoCruzamento cruzamento,
        PoliticaAnalise politica)
    {
        var verificados = cruzamento.TiposVerificados.ToHashSet();

        return politica.Regras
            .Where(r => !verificados.Contains(r.Tipo))
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
            texto.AppendLine($"  Camada: {grupo.CamadaNome} ({grupo.Origem})");
            texto.AppendLine(
                $"  Sobreposição: {Formatar(grupo.AreaTotalHa)} ha " +
                $"({Formatar(grupo.PercentualTotal)}% do imóvel) em {grupo.Quantidade} polígono(s)");

            if (grupo.Rotulos.Count > 0)
            {
                var reticencias = grupo.Quantidade > grupo.Rotulos.Count ? ", ..." : string.Empty;
                texto.AppendLine($"  Feições: {string.Join(", ", grupo.Rotulos)}{reticencias}");
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
