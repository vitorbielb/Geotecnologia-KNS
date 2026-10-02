using System.Text.RegularExpressions;

namespace GeotecnologiaKNS.Geo;

/// <summary>
/// Normalização e validação do código do CAR.
/// Formato: UF-CodigoIbge-Hash, ex.: "MT-5107925-8E1F0A2B...".
/// O usuário digita com espaços, minúsculas e às vezes com pontos; tudo isso
/// precisa convergir para uma única chave, senão a busca na base falha em silêncio.
/// </summary>
public static class CodigoCar
{
    private static readonly Regex Formato = new(
        @"^(?<uf>[A-Z]{2})-(?<ibge>\d{7})-(?<hash>[A-F0-9]{32,})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Devolve o código em forma canônica, ou null se não for um código de CAR plausível.
    /// </summary>
    public static string? Normalizar(string? codigoCar)
    {
        if (string.IsNullOrWhiteSpace(codigoCar))
        {
            return null;
        }

        var candidato = new string(codigoCar
            .Where(c => !char.IsWhiteSpace(c) && c != '.')
            .ToArray())
            .ToUpperInvariant();

        return Formato.IsMatch(candidato) ? candidato : null;
    }

    public static bool EhValido(string? codigoCar) => Normalizar(codigoCar) is not null;

    /// <summary>Sigla da UF embutida no código, ou null se o código for inválido.</summary>
    public static string? ExtrairUf(string? codigoCar)
    {
        var normalizado = Normalizar(codigoCar);
        return normalizado is null ? null : Formato.Match(normalizado).Groups["uf"].Value;
    }

    /// <summary>Código IBGE do município embutido no código, ou null se o código for inválido.</summary>
    public static string? ExtrairCodigoIbge(string? codigoCar)
    {
        var normalizado = Normalizar(codigoCar);
        return normalizado is null ? null : Formato.Match(normalizado).Groups["ibge"].Value;
    }
}
