namespace GeotecnologiaKNS.Geo.Entities;

/// <summary>
/// A que lista restritiva o registro pertence.
/// </summary>
public enum TipoRestricao
{
    Desconhecida = 0,

    /// <summary>Termo de embargo do IBAMA em nome da pessoa.</summary>
    EmbargoAmbiental = 1,

    /// <summary>Cadastro de Empregadores (trabalho análogo a escravo).</summary>
    TrabalhoEscravo = 2
}

/// <summary>
/// Restrição que recai sobre uma pessoa, e não sobre uma área.
/// </summary>
/// <remarks>
/// Existe porque o cruzamento geográfico não alcança tudo. Dos termos de
/// embargo do IBAMA, quase metade não tem área delimitada — são cerca de 50 mil
/// embargos que nenhum cruzamento de polígono encontra, por mais camadas que se
/// carregue. Eles só aparecem pelo CPF ou CNPJ do autuado.
///
/// E há o caso que a geografia nunca resolveria: o fornecedor cujo imóvel está
/// limpo, mas que responde por embargo em outra fazenda. Para quem compra, é a
/// mesma pessoa.
/// </remarks>
public class RestricaoDocumento
{
    public long Id { get; set; }

    /// <summary>CPF ou CNPJ, só dígitos.</summary>
    /// <remarks>
    /// Normalizado na gravação e na consulta. A origem publica ora com
    /// pontuação, ora sem, e comparar texto cru deixaria passar o mesmo
    /// documento escrito de outro jeito.
    /// </remarks>
    public string Documento { get; set; } = string.Empty;

    public string? NomeTitular { get; set; }

    public TipoRestricao Tipo { get; set; }

    /// <summary>Órgão e base de onde veio.</summary>
    public string Origem { get; set; } = string.Empty;

    /// <summary>Identificação do ato na origem — o número do TAD, por exemplo.</summary>
    public string? Referencia { get; set; }

    public string? Municipio { get; set; }

    public string? Uf { get; set; }

    /// <summary>Data do ato, como publicada na origem.</summary>
    public string? DataRestricao { get; set; }

    /// <summary>
    /// Indica se este registro também tem área delimitada na camada geográfica.
    /// </summary>
    /// <remarks>
    /// Serve ao laudo: dizer que o embargo encontrado pelo documento é o mesmo
    /// já apontado pela sobreposição evita contar duas vezes a mesma restrição.
    /// </remarks>
    public bool TemGeometria { get; set; }

    /// <summary>Versão da carga a que este registro pertence.</summary>
    public int Versao { get; set; }

    public DateTime CarregadoEm { get; set; } = DateTime.UtcNow;
}
