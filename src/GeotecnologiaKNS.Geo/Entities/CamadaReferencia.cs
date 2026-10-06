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

    /// <summary>
    /// Indústria dona da camada. Nulo para as camadas públicas, que valem
    /// para todas.
    /// </summary>
    /// <remarks>
    /// As camadas públicas — embargos, terras indígenas, PRODES — são as mesmas
    /// para todo mundo. Mas a indústria também pode subir perímetros próprios,
    /// e aí o isolamento deixa de ser detalhe de arquitetura: as indústrias
    /// atendidas são concorrentes diretas entre si, e a lista de áreas que uma
    /// delas resolveu bloquear diz onde ela compra e onde parou de comprar.
    /// Vazar isso seria entregar estratégia comercial à concorrente.
    ///
    /// Por isso o cruzamento exige o tenant explicitamente, em vez de assumir
    /// um padrão: esquecer de passar não pode significar "mostra tudo".
    /// </remarks>
    public int? TenantId { get; set; }

    public DateTime? AtualizadaEm { get; set; }

    public int TotalFeicoes { get; set; }

    /// <summary>
    /// Região que a camada de fato cobre. Nulo significa cobertura nacional.
    /// </summary>
    /// <remarks>
    /// Existe porque a guarda de cobertura estava um nível acima do necessário
    /// e deixava passar justamente o erro que ela foi feita para impedir. Ela
    /// perguntava "existe camada deste tipo?" — e o PRODES da Amazônia responde
    /// que sim para um imóvel de Goiás, onde não tem um polígono sequer. A
    /// regra se dizia avaliada, o laudo listava a camada entre as verificadas,
    /// e o imóvel saía liberado por omissão. Dos 1,25 milhão de imóveis da
    /// base, 334 mil estão em Goiás e Mato Grosso do Sul, fora de qualquer
    /// camada de desmatamento que havia carregada.
    ///
    /// A abrangência é <b>declarada</b> no catálogo, não deduzida das feições.
    /// Deduzir seria circular: camada de desmatamento só tem polígono onde
    /// houve desmatamento, e a ausência deles tanto pode significar "floresta
    /// intacta" quanto "o satélite nunca olhou para cá". Só a origem sabe qual
    /// das duas, e é ela quem declara.
    /// </remarks>
    public Geometry? Abrangencia { get; set; }

    /// <summary>
    /// Primeiro ano que a camada alcança. Nulo quando ela não é datada.
    /// </summary>
    /// <remarks>
    /// O equivalente temporal da abrangência, e tão necessário quanto: o PRODES
    /// carregado tinha só o ano de 2024, enquanto a regra DES-001 se chama
    /// "desmatamento consolidado a partir de 2008". Dezesseis anos que a regra
    /// dizia examinar e não tinha como encontrar.
    ///
    /// Vem medido das feições gravadas, e não declarado, porque aqui o dado
    /// sabe a resposta: o menor ano presente é o começo da cobertura. Declarar
    /// abriria espaço para a declaração divergir do arquivo, que é o defeito
    /// que este campo existe para fechar.
    ///
    /// É um inteiro, e não uma data, porque ano é o que as regras cortam — e
    /// porque guardar instante aqui já deu errado uma vez: como
    /// <c>timestamptz</c>, 1º de janeiro de 2025 em UTC volta como 2024 numa
    /// sessão em Brasília, e a camada se declarava um ano mais antiga do que é.
    /// Errar para mais velho é errar para o lado perigoso: faz a regra se dizer
    /// avaliada sobre um período que a camada não cobre.
    /// </remarks>
    public int? CobreDesdeAno { get; set; }

    /// <summary>Versão das feições que a análise enxerga.</summary>
    /// <remarks>
    /// A recarga grava a versão seguinte ao lado da atual e só troca este
    /// número no fim, depois de conferir que o resultado é íntegro. Sem isso a
    /// carga apagaria a camada antes de saber se a nova presta, e uma quebra no
    /// meio deixaria a análise rodando contra dados pela metade — dizendo
    /// "liberado" sobre o que não chegou a ser verificado.
    /// </remarks>
    public int VersaoAtual { get; set; }

    /// <summary>
    /// De quantos em quantos dias a fonte deve ser recarregada.
    /// </summary>
    /// <remarks>
    /// Embargos e alertas mudam quase diariamente; PRODES sai uma vez por ano.
    /// Sem isso, ninguém sabe que uma camada envelheceu — e camada velha não
    /// avisa, só deixa de encontrar o que passou a existir.
    /// </remarks>
    public int? PeriodicidadeDias { get; set; }

    /// <summary>Quando a camada deveria ser recarregada.</summary>
    public DateTime? VenceEm =>
        PeriodicidadeDias.HasValue && AtualizadaEm.HasValue
            ? AtualizadaEm.Value.AddDays(PeriodicidadeDias.Value)
            : null;

    /// <summary>Indica se a camada passou do prazo de recarga.</summary>
    public bool Vencida => VenceEm.HasValue && VenceEm.Value < DateTime.UtcNow;

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

    /// <summary>Versão da carga a que esta feição pertence.</summary>
    public int Versao { get; set; }

    /// <summary>
    /// Ano do fato que a feição registra, quando a origem o informa.
    /// </summary>
    /// <remarks>
    /// O corte temporal de uma regra ("a partir de 2008") precisa recair sobre
    /// o fato, não sobre a carga. Antes isso era lido do ano da camada inteira,
    /// o que só funcionava enquanto cada camada guardasse um único ano — e
    /// deixava de funcionar no instante em que o PRODES passasse a trazer de
    /// 2008 em diante, porque aí todo polígono responderia pelo mesmo ano.
    /// </remarks>
    public int? Ano { get; set; }
}
