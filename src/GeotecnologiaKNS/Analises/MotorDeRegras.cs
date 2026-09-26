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
    string Parecer)
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

        return new ResultadoAvaliacao(status, achados, MontarParecer(cruzamento, achados, status, politica));
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

        texto.AppendLine($"Resultado: {status.ToString().ToUpperInvariant()}");
        texto.AppendLine();
        texto.AppendLine($"Ocorrências ({achados.Count}):");

        foreach (var achado in achados)
        {
            texto.AppendLine();
            texto.AppendLine($"[{achado.Severidade.ToString().ToUpperInvariant()}] {achado.CodigoRegra} — {achado.Descricao}");
            texto.AppendLine($"  Camada: {achado.CamadaNome} ({achado.Origem})");

            if (!string.IsNullOrWhiteSpace(achado.Rotulo))
            {
                texto.AppendLine($"  Feição: {achado.Rotulo}");
            }

            texto.AppendLine(
                $"  Sobreposição: {Formatar(achado.AreaSobrepostaHa)} ha " +
                $"({Formatar(achado.PercentualDoImovel)}% do imóvel)");

            if (!string.IsNullOrWhiteSpace(achado.Fundamento))
            {
                texto.AppendLine($"  Fundamento: {achado.Fundamento}");
            }
        }

        return texto.ToString();
    }

    private static string Formatar(double valor) =>
        valor.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
