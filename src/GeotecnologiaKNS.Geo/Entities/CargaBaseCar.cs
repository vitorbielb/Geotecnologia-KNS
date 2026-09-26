namespace GeotecnologiaKNS.Geo.Entities;

/// <summary>
/// Registro de uma carga da base do CAR.
/// Existe para que todo laudo possa dizer exatamente de qual versão da base
/// o perímetro veio — sem isso não há como defender um bloqueio depois.
/// </summary>
public class CargaBaseCar
{
    public long Id { get; set; }

    /// <summary>De onde veio o arquivo (ex.: "MapBiomas/CAR-Camada-Completa", "SICAR/MT/Sinop").</summary>
    public string Origem { get; set; } = default!;

    /// <summary>Nome do arquivo processado.</summary>
    public string Arquivo { get; set; } = default!;

    /// <summary>Hash do arquivo de origem, para detectar reprocessamento do mesmo conteúdo.</summary>
    public string? HashArquivo { get; set; }

    /// <summary>UF abrangida pela carga, quando a carga é regional.</summary>
    public string? Uf { get; set; }

    public DateTime IniciadaEm { get; set; } = DateTime.UtcNow;

    public DateTime? ConcluidaEm { get; set; }

    public int RegistrosLidos { get; set; }

    public int RegistrosGravados { get; set; }

    public int RegistrosDescartados { get; set; }

    public StatusCarga Status { get; set; } = StatusCarga.EmAndamento;

    public string? Erro { get; set; }

    public List<ImovelCar> Imoveis { get; set; } = new();
}

public enum StatusCarga
{
    EmAndamento = 0,
    Concluida = 1,
    Falhou = 2
}
