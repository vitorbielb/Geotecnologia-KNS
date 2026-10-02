using System.Globalization;

namespace GeotecnologiaKNS.Geo;

/// <summary>
/// Formatação de números para quem lê em português.
/// </summary>
/// <remarks>
/// Existe porque a cultura do processo não é confiável aqui. A aplicação define
/// pt-BR no pipeline de requisição, mas a recarga de camadas e o processamento
/// de análises rodam em serviço de fundo, fora dele — num servidor Linux a
/// cultura ali é a invariante, e "57.843 feições" vira "57,843 feições".
///
/// O defeito apareceu no CI: o mesmo teste passava no Windows em português e
/// falhava no runner. A mensagem que ele compara é a que explica a um operador
/// por que uma recarga foi recusada, e trocar ponto por vírgula numa contagem
/// de dezenas de milhares muda o que a frase diz.
/// </remarks>
public static class Formatos
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Contagem inteira com separador de milhar: 57.843.</summary>
    public static string Quantidade(int valor) => valor.ToString("N0", PtBr);

    /// <summary>Número com duas casas: 3.073,23.</summary>
    public static string Decimal(double valor) => valor.ToString("N2", PtBr);
}
