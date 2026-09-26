using NetTopologySuite.Geometries;

namespace GeotecnologiaKNS.Geo.Entities;

/// <summary>
/// Imóvel rural da base pública do CAR (camada AREA_IMOVEL do SICAR).
/// É base de referência compartilhada: não pertence a nenhum tenant.
/// </summary>
public class ImovelCar
{
    /// <summary>Código do CAR, ex.: "MT-5103403-A1B2...". Chave natural.</summary>
    public string CodigoCar { get; set; } = default!;

    /// <summary>Perímetro do imóvel em WGS84 (SRID 4326).</summary>
    public Geometry Perimetro { get; set; } = default!;

    /// <summary>Centroide pré-calculado, para centralizar o mapa sem recalcular.</summary>
    public Point? Centroide { get; set; }

    /// <summary>Área declarada no CAR, em hectares.</summary>
    public double? AreaHa { get; set; }

    /// <summary>Área calculada a partir da geometria, em hectares (divergência é sinal de alerta).</summary>
    public double? AreaCalculadaHa { get; set; }

    public string? Municipio { get; set; }

    /// <summary>Sigla da UF, ex.: "MT".</summary>
    public string? Uf { get; set; }

    /// <summary>Código IBGE do município, quando disponível na origem.</summary>
    public string? CodigoIbge { get; set; }

    /// <summary>Situação declarada na base (AT/PE/CA/SUSPENSO...), conforme a origem.</summary>
    public string? Situacao { get; set; }

    /// <summary>Tipo do imóvel na base (IRU, AST, PCT).</summary>
    public string? Tipo { get; set; }

    /// <summary>Data de atualização informada pela origem.</summary>
    public DateTime? AtualizadoEmOrigem { get; set; }

    /// <summary>Identificador da carga que trouxe este registro.</summary>
    public long CargaId { get; set; }

    public CargaBaseCar? Carga { get; set; }
}
