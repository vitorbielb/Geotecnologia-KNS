using GeotecnologiaKNS.Geo.Entities;

namespace GeotecnologiaKNS.Analises;

/// <summary>
/// O que uma regra faz quando dispara.
/// </summary>
public enum Severidade
{
    /// <summary>Registra no laudo sem alterar o veredito.</summary>
    Informativo = 0,

    Alerta = 1,

    Bloqueio = 2
}

/// <summary>
/// Regra de conformidade: uma condição sobre sobreposições de um tipo de camada
/// e a consequência quando ela é satisfeita.
/// </summary>
/// <remarks>
/// As regras são dados, não código, para que cada indústria tenha o próprio
/// corte sem recompilar o sistema — o que uma trata como bloqueio, outra trata
/// como alerta.
/// </remarks>
public class RegraAnalise
{
    public string Codigo { get; set; } = default!;

    public string Descricao { get; set; } = default!;

    public TipoCamada Tipo { get; set; }

    /// <summary>
    /// Quando preenchido, a regra examina uma lista restritiva por CPF/CNPJ em
    /// vez de sobreposição geográfica.
    /// </summary>
    /// <remarks>
    /// Mora na mesma entidade de propósito. São duas perguntas diferentes — "o
    /// imóvel toca uma área restrita?" e "a pessoa responde por restrição?" —,
    /// mas ambas são regras de conformidade que a indústria calibra do mesmo
    /// jeito: código, descrição, severidade e fundamento. Separar em duas
    /// hierarquias duplicaria a tela de política, a persistência e o laudo para
    /// ganhar pouco. Quando a regra é por documento, Tipo e os limiares de área
    /// não se aplicam.
    /// </remarks>
    public TipoRestricao? Restricao { get; set; }

    /// <summary>Indica se esta regra examina lista restritiva, e não camada.</summary>
    public bool EhPorDocumento => Restricao.HasValue;

    public Severidade Severidade { get; set; }

    /// <summary>Área mínima sobreposta, em hectares, para a regra disparar.</summary>
    public double AreaMinimaHa { get; set; }

    /// <summary>Percentual mínimo do imóvel sobreposto para a regra disparar.</summary>
    public double PercentualMinimo { get; set; }

    /// <summary>
    /// Quando informado, a regra só considera camadas a partir deste ano.
    /// Usado no corte temporal de desmatamento.
    /// </summary>
    public int? AnoMinimo { get; set; }

    /// <summary>Justificativa legal ou contratual, citada no laudo.</summary>
    public string? Fundamento { get; set; }

    /// <summary>
    /// Avalia se a sobreposição satisfaz esta regra.
    /// </summary>
    public bool Satisfeita(Sobreposicao sobreposicao)
    {
        ArgumentNullException.ThrowIfNull(sobreposicao);

        if (sobreposicao.Tipo != Tipo)
        {
            return false;
        }

        if (AnoMinimo.HasValue && (sobreposicao.AnoReferencia is null || sobreposicao.AnoReferencia < AnoMinimo))
        {
            return false;
        }

        // Os dois limiares são independentes: uma sobreposição pequena num
        // imóvel pequeno pode ser proporcionalmente grave, e vice-versa.
        // Basta um deles ser atingido.
        var atingiuArea = AreaMinimaHa > 0 && sobreposicao.AreaSobrepostaHa >= AreaMinimaHa;
        var atingiuPercentual = PercentualMinimo > 0 && sobreposicao.PercentualDoImovel >= PercentualMinimo;

        if (AreaMinimaHa <= 0 && PercentualMinimo <= 0)
        {
            // Sem limiar: qualquer sobreposição dispara.
            return true;
        }

        return atingiuArea || atingiuPercentual;
    }
}

/// <summary>
/// Conjunto de regras aplicado a um tenant.
/// </summary>
public class PoliticaAnalise
{
    public string Nome { get; set; } = default!;

    public List<RegraAnalise> Regras { get; set; } = new();

    /// <summary>
    /// Política aplicada quando a indústria não definiu a própria.
    /// </summary>
    /// <remarks>
    /// Reproduz o protocolo usual de monitoramento de cadeia produtiva no Brasil.
    /// Os limiares são ponto de partida e devem ser revisados por quem responde
    /// pela conformidade — não são norma.
    /// </remarks>
    public static PoliticaAnalise Padrao() => new()
    {
        Nome = "Protocolo padrão",
        Regras = new List<RegraAnalise>
        {
            new()
            {
                Codigo = "EMB-001",
                Descricao = "Sobreposição com área embargada",
                Tipo = TipoCamada.EmbargoAmbiental,
                Severidade = Severidade.Bloqueio,
                Fundamento = "Área embargada por infração ambiental; aquisição vedada."
            },
            new()
            {
                // Pega o que a geografia não alcança, e são dois casos: o
                // embargo sem área delimitada — quase metade dos termos do
                // IBAMA — e o produtor cujo imóvel está limpo mas que responde
                // por embargo em outra fazenda. Para quem compra, é a mesma
                // pessoa.
                Codigo = "EMB-002",
                Descricao = "Embargo ambiental em nome do produtor",
                Restricao = TipoRestricao.EmbargoAmbiental,
                Severidade = Severidade.Bloqueio,
                Fundamento = "Autuado com termo de embargo vigente pelo IBAMA."
            },
            new()
            {
                Codigo = "TI-001",
                Descricao = "Sobreposição com Terra Indígena",
                Tipo = TipoCamada.TerraIndigena,
                Severidade = Severidade.Bloqueio,
                Fundamento = "Terras indígenas são bens da União, de posse exclusiva dos povos indígenas."
            },
            new()
            {
                Codigo = "QUI-001",
                Descricao = "Sobreposição com território quilombola",
                Tipo = TipoCamada.TerritorioQuilombola,
                Severidade = Severidade.Bloqueio,
                Fundamento = "Território quilombola titulado ou em processo de titulação."
            },
            new()
            {
                Codigo = "UC-001",
                Descricao = "Sobreposição com Unidade de Conservação",
                Tipo = TipoCamada.UnidadeConservacao,
                Severidade = Severidade.Bloqueio,
                Fundamento = "Unidade de conservação de proteção integral ou uso sustentável."
            },
            new()
            {
                Codigo = "DES-001",
                Descricao = "Desmatamento consolidado a partir de 2008",
                Tipo = TipoCamada.DesmatamentoConsolidado,
                Severidade = Severidade.Bloqueio,
                AnoMinimo = 2008,
                Fundamento = "Corte temporal usual dos compromissos de cadeia produtiva."
            },
            new()
            {
                Codigo = "ALE-001",
                Descricao = "Alerta de desmatamento no perímetro",
                Tipo = TipoCamada.AlertaDesmatamento,
                Severidade = Severidade.Alerta,
                Fundamento = "Alerta ainda não consolidado; exige verificação."
            },
            new()
            {
                Codigo = "ASS-001",
                Descricao = "Sobreposição com assentamento rural",
                Tipo = TipoCamada.AssentamentoRural,
                Severidade = Severidade.Alerta,
                PercentualMinimo = 5,
                Fundamento = "Sobreposição relevante com projeto de assentamento."
            },
            new()
            {
                Codigo = "OUT-001",
                Descricao = "Sobreposição com outro perímetro restritivo",
                Tipo = TipoCamada.OutroPerimetro,
                Severidade = Severidade.Alerta
            }
        }
    };
}
