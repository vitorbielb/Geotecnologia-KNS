using NetTopologySuite.Geometries;

namespace GeotecnologiaKNS.Geo.Entities;

/// <summary>
/// Natureza da restrição que a camada representa.
/// </summary>
/// <remarks>
/// O motor de regras decide por este tipo, não pelo nome da camada — assim a
/// mesma regra vale para o embargo federal do IBAMA e para o embargo estadual
/// de uma SEMA, que chegam como camadas diferentes.
/// </remarks>
public enum TipoCamada
{
    Desconhecida = 0,

    /// <summary>Área embargada pelo IBAMA ou por órgão estadual.</summary>
    EmbargoAmbiental = 1,

    TerraIndigena = 2,

    UnidadeConservacao = 3,

    TerritorioQuilombola = 4,

    AssentamentoRural = 5,

    /// <summary>Desmatamento consolidado (PRODES/INPE).</summary>
    DesmatamentoConsolidado = 6,

    /// <summary>Alerta de desmatamento (DETER/INPE, MapBiomas Alerta).</summary>
    AlertaDesmatamento = 7,

    /// <summary>Limite de bioma.</summary>
    Bioma = 8,

    /// <summary>Qualquer outro perímetro restritivo definido pela indústria.</summary>
    OutroPerimetro = 9
}

/// <summary>
/// Uma camada geoespacial de referência carregada no sistema
/// (embargos do IBAMA, terras indígenas da FUNAI, PRODES de um ano, etc.).
/// </summary>
public class CamadaReferencia
{
    public int Id { get; set; }

    /// <summary>Chave estável usada nas regras e na ingestão, ex.: "ibama-embargos".</summary>
    public string Chave { get; set; } = default!;

    public string Nome { get; set; } = default!;

    public TipoCamada Tipo { get; set; }

    /// <summary>Órgão ou plataforma de origem, ex.: "IBAMA/Dados Abertos".</summary>
    public string Origem { get; set; } = default!;

    /// <summary>
    /// Ano de referência, quando a camada é datada (PRODES 2020, por exemplo).
    /// </summary>
    public int? AnoReferencia { get; set; }

    /// <summary>Camada inativa não entra no cruzamento, mas o histórico é preservado.</summary>
    public bool Ativa { get; set; } = true;

    public DateTime? AtualizadaEm { get; set; }

    public int TotalFeicoes { get; set; }

    public List<FeicaoReferencia> Feicoes { get; set; } = new();
}

/// <summary>
/// Um polígono de uma camada de referência.
/// </summary>
public class FeicaoReferencia
{
    public long Id { get; set; }

    public int CamadaId { get; set; }

    public CamadaReferencia? Camada { get; set; }

    public Geometry Geometria { get; set; } = default!;

    /// <summary>
    /// Atributos da origem preservados como JSON: número do termo de embargo,
    /// nome da terra indígena, data do alerta. O laudo cita estes valores.
    /// </summary>
    public string? AtributosJson { get; set; }

    /// <summary>Rótulo legível para o laudo, extraído dos atributos na ingestão.</summary>
    public string? Rotulo { get; set; }
}
