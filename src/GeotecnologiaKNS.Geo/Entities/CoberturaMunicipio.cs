namespace GeotecnologiaKNS.Geo.Entities;

/// <summary>
/// Município cuja base do CAR já foi carregada.
/// </summary>
/// <remarks>
/// Sem este registro o sistema não sabe o que ele não sabe: "CAR não
/// encontrado" misturava falha de cobertura, que é problema de quem opera o
/// serviço, com dado ruim do cliente. São situações com responsáveis e
/// soluções diferentes, e num produto vendido a várias indústrias o suporte
/// precisa distinguir uma da outra.
///
/// É preenchido pela importação, a partir dos municípios efetivamente vistos
/// no arquivo — e não deduzido da existência de imóveis, que confundiria
/// município sem cadastro com município não carregado.
/// </remarks>
public class CoberturaMunicipio
{
    /// <summary>Código IBGE do município, com sete dígitos. Chave natural.</summary>
    public string CodigoIbge { get; set; } = default!;

    public string Uf { get; set; } = default!;

    /// <summary>Nome do município, quando a origem o informa.</summary>
    public string? Municipio { get; set; }

    /// <summary>Quantos imóveis deste município entraram na última carga.</summary>
    public int Imoveis { get; set; }

    /// <summary>Carga que cobriu o município pela última vez.</summary>
    public long CargaId { get; set; }

    public CargaBaseCar? Carga { get; set; }

    public DateTime CobertoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Consulta a um município que ainda não foi carregado.
/// </summary>
/// <remarks>
/// É a fila de demanda real: mostra quais municípios os clientes estão
/// pedindo, para que a carga seja guiada por uso e não por tentativa de mapear
/// o país. Guarda o inquilino para permitir priorizar o que mais gente pede.
/// </remarks>
public class LacunaCobertura
{
    public string CodigoIbge { get; set; } = default!;

    /// <summary>Indústria que consultou. Zero quando a consulta veio de ferramenta administrativa.</summary>
    public int TenantId { get; set; }

    public string Uf { get; set; } = default!;

    /// <summary>Último código consultado, útil para conferir o caso concreto.</summary>
    public string? UltimoCodigoCar { get; set; }

    public int Consultas { get; set; }

    public DateTime PrimeiraEm { get; set; } = DateTime.UtcNow;

    public DateTime UltimaEm { get; set; } = DateTime.UtcNow;
}
