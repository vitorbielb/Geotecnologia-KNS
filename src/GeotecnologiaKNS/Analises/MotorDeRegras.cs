using System.Text;
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

public record ResultadoAvaliacao(
    Status Status,
    IReadOnlyList<Achado> Achados,
    string Parecer,
    string Resumo)
{
    public bool TemBloqueio => Achados.Any(a => a.Severidade == Severidade.Bloqueio);
    public bool TemAlerta => Achados.Any(a => a.Severidade == Severidade.Alerta);
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

        var status = DeterminarStatus(achados);

        return new ResultadoAvaliacao(
            status,
            achados,
            MontarParecer(cruzamento, achados, status, politica),
            MontarResumo(achados, status));
    }

    /// <summary>
    /// Resumo curto do veredito, para o campo Parecer da solicitação.
    /// </summary>
    /// <remarks>
    /// O laudo completo fica em AnaliseAutomatica.Parecer, que não tem limite de
    /// tamanho. A coluna da solicitação tem 2000 caracteres e um imóvel com
    /// dezenas de sobreposições estourava esse limite, derrubando a gravação.
    /// </remarks>
    private static string MontarResumo(IReadOnlyList<Achado> achados, Status status)
    {
        if (achados.Count == 0)
        {
            return "Análise automática: LIBERADO. Nenhuma sobreposição restritiva nas camadas verificadas.";
        }

        var texto = new StringBuilder();
        texto.Append($"Análise automática: {status.ToString().ToUpperInvariant()}. ");

        var porRegra = AgruparPorRegra(achados);

        texto.Append(string.Join("; ", porRegra.Select(g =>
            $"{g.CodigoRegra} {g.CamadaNome} — {Formatar(g.AreaTotalHa)} ha ({Formatar(g.PercentualTotal)}%)")));

        texto.Append(". Laudo completo na análise.");

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
    /// Um único bloqueio bloqueia. Nenhum achado libera. O resto é alerta.
    /// </summary>
    private static Status DeterminarStatus(IReadOnlyList<Achado> achados)
    {
        if (achados.Any(a => a.Severidade == Severidade.Bloqueio))
        {
            return Status.Bloqueado;
        }

        if (achados.Any(a => a.Severidade == Severidade.Alerta))
        {
            return Status.Alerta;
        }

        return Status.Liberado;
    }

    private static string MontarParecer(
        ResultadoCruzamento cruzamento,
        IReadOnlyList<Achado> achados,
        Status status,
        PoliticaAnalise politica)
    {
        var texto = new StringBuilder();

        texto.AppendLine($"Análise automática — {politica.Nome}");
        texto.AppendLine($"CAR: {cruzamento.CodigoCar}");
        texto.AppendLine($"Área do imóvel: {Formatar(cruzamento.AreaImovelHa)} ha");
        texto.AppendLine($"Executada em: {cruzamento.ExecutadoEm:dd/MM/yyyy HH:mm} (UTC)");
        texto.AppendLine();

        if (achados.Count == 0)
        {
            texto.AppendLine("Resultado: LIBERADO");
            texto.AppendLine("Nenhuma sobreposição restritiva encontrada nas camadas verificadas.");
            return texto.ToString();
        }

        var grupos = AgruparPorRegra(achados);

        texto.AppendLine($"Resultado: {status.ToString().ToUpperInvariant()}");
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

        return texto.ToString();
    }

    private static string Formatar(double valor) =>
        valor.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
