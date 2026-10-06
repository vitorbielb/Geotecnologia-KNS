using System.Text.Json;
using GeotecnologiaKNS.Geo.Services;

namespace GeotecnologiaKNS.Laudos;

/// <summary>Uma regra do protocolo, como estava quando a análise rodou.</summary>
public record RegraDoRetrato(
    string Codigo,
    string Descricao,
    string Tipo,
    string Severidade,
    double AreaMinimaHa,
    double PercentualMinimo,
    int? AnoMinimo,
    string? Fundamento);

/// <summary>Uma camada consultada, com origem e data da carga.</summary>
public record CamadaDoLaudo(string Descricao);

/// <summary>Um fornecedor indireto declarado para o imóvel.</summary>
public record FornecedorDoLaudo(
    string CodigoCar,
    string? NomeProdutor,
    string? Documento,
    string? Origem,
    DateTime DeclaradoEm);

/// <summary>
/// Tudo que o laudo precisa dizer, reunido num lugar só.
/// </summary>
/// <remarks>
/// Montado a partir do que ficou gravado na análise, e não recalculado: o laudo
/// precisa descrever o que de fato aconteceu naquela execução, com as regras e
/// as camadas daquele dia. Refazer a análise na hora de emitir produziria um
/// documento que não corresponde ao veredito que já foi comunicado — e um laudo
/// que muda sozinho não serve para defender nada.
/// </remarks>
public class DadosDoLaudo
{
    public required AnaliseAutomatica Analise { get; init; }
    public required Solicitacao Solicitacao { get; init; }
    public required Propriedade Propriedade { get; init; }
    public Produtor? Produtor { get; init; }
    public Industria? Industria { get; init; }

    /// <summary>Fornecedores indiretos declarados para o imóvel.</summary>
    public IReadOnlyList<FornecedorDoLaudo> CadeiaIndireta { get; init; } = [];

    /// <summary>Imagem do mapa com o perímetro e as sobreposições, em PNG.</summary>
    public byte[]? Mapa { get; init; }

    /// <summary>Por que o mapa não pôde ser gerado, quando for o caso.</summary>
    public string? MapaIndisponivel { get; init; }

    /// <summary>
    /// Identificador de conferência, derivado do conteúdo da análise.
    /// </summary>
    /// <remarks>
    /// Serve para quem recebe o documento confirmar que ele corresponde a uma
    /// análise registrada, e para detectar um PDF adulterado: o mesmo laudo
    /// emitido duas vezes dá o mesmo código, e qualquer alteração no veredito,
    /// na data ou nas ocorrências dá outro.
    ///
    /// Não é assinatura digital e o laudo diz isso. Assinatura exigiria
    /// certificado ICP-Brasil, que é decisão de quem assina, não do sistema.
    /// </remarks>
    public required string CodigoDeConferencia { get; init; }

    public string Numero => $"{Analise.IniciadaEm:yyyy}/{Analise.Id:D6}";

    /// <summary>As camadas consultadas, uma por linha, como foram gravadas.</summary>
    public IReadOnlyList<CamadaDoLaudo> Camadas =>
        (Analise.CamadasVerificadas ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => new CamadaDoLaudo(x))
            .ToList();

    /// <summary>As regras que não puderam ser aplicadas, uma por linha.</summary>
    public IReadOnlyList<string> NaoAvaliadas =>
        (Analise.RegrasNaoAvaliadas ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>
    /// O protocolo como estava na execução, lido do retrato gravado.
    /// </summary>
    /// <remarks>
    /// Do retrato, e não da política atual: afrouxar uma regra depois não pode
    /// reescrever um bloqueio já emitido, e é essa garantia que torna o laudo
    /// defensável meses depois.
    /// </remarks>
    public IReadOnlyList<RegraDoRetrato> Protocolo
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Analise.PoliticaAplicada))
            {
                return [];
            }

            try
            {
                using var documento = JsonDocument.Parse(Analise.PoliticaAplicada);

                if (!documento.RootElement.TryGetProperty("Regras", out var regras))
                {
                    return [];
                }

                return regras.EnumerateArray()
                    .Select(r => new RegraDoRetrato(
                        Texto(r, "Codigo"),
                        Texto(r, "Descricao"),
                        Texto(r, "Tipo"),
                        Texto(r, "Severidade"),
                        Decimal(r, "AreaMinimaHa"),
                        Decimal(r, "PercentualMinimo"),
                        Inteiro(r, "AnoMinimo"),
                        Texto(r, "Fundamento")))
                    .ToList();
            }
            catch (JsonException)
            {
                // Retrato ilegível não pode impedir a emissão: o resto do laudo
                // continua verdadeiro, e a seção some em vez de o documento
                // deixar de existir.
                return [];
            }
        }
    }

    private static string Texto(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static double Decimal(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : 0;

    private static int? Inteiro(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : null;
}
