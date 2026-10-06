using GeotecnologiaKNS.Geo.Entities;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Como o arquivo publicado pela origem deve ser lido.
/// </summary>
public enum FormatoDaFonte
{
    /// <summary>Zip contendo um shapefile.</summary>
    ShapefileEmZip = 0,

    /// <summary>Zip contendo um GeoJSON.</summary>
    GeoJsonEmZip = 1,

    /// <summary>CSV com a geometria em WKT numa coluna (IBAMA).</summary>
    CsvEmbargo = 2
}

/// <param name="Url">
/// Endereço exato de download. Pode conter <c>{corte12m}</c>, trocado pela data
/// de doze meses atrás no momento da recarga.
/// </param>
/// <param name="CorpoJson">
/// Quando preenchido, o download é POST com este corpo. Só o CNUC precisa.
/// </param>
/// <param name="PeriodicidadeDias">
/// De quanto em quanto tempo a camada é rebaixada. Vem do ritmo da origem, não
/// do desejo: adiantar a recarga de uma base anual só gasta banda.
/// </param>
/// <param name="Biomas">
/// Biomas que a camada cobre. Nulo significa cobertura nacional.
/// </param>
/// <param name="ChaveDeOrdenacao">
/// Campo pelo qual paginar o download. Nulo baixa em uma requisição só.
/// </param>
/// <remarks>
/// A abrangência é declarada aqui, e não deduzida das feições carregadas,
/// porque deduzir seria circular: uma camada de desmatamento só tem polígono
/// onde houve desmatamento, e a ausência deles tanto pode significar "floresta
/// intacta" quanto "esta camada nunca olhou para cá". Só quem publica sabe
/// qual das duas.
///
/// Omitir este campo numa camada regional é o defeito que custou caro: o PRODES
/// da Amazônia respondia "sim, estou carregado" para um imóvel de Goiás, a
/// regra se dizia avaliada e o laudo liberava por omissão.
/// </remarks>
public record FonteDeCamada(
    string Chave,
    string Nome,
    TipoCamada Tipo,
    string Origem,
    FormatoDaFonte Formato,
    int PeriodicidadeDias,
    string Url,
    string? CorpoJson = null,
    int? AnoReferencia = null,
    FonteDeCamada? Reserva = null,
    IReadOnlyList<string>? Biomas = null,
    string? ChaveDeOrdenacao = null)
{
    /// <summary>A camada é baixada em páginas.</summary>
    /// <remarks>
    /// Necessário acima do teto que o GeoServer impõe por requisição — o do
    /// INPE declara 50.000 em <c>CountDefault</c>. Acima disso ele devolve as
    /// primeiras 50.000 e <b>200 OK</b>: nada falha, o zip é válido, o
    /// importador grava, e a camada entra no ar com 6% do que deveria.
    ///
    /// Foi o que aconteceu com o PRODES da Amazônia: 50.000 polígonos gravados
    /// de 802.277 existentes, sem um erro em lugar nenhum.
    /// </remarks>
    public bool EhPaginada => ChaveDeOrdenacao is not null;

    public bool EhPost => CorpoJson is not null;

    /// <summary>A fonte e, em seguida, as reservas dela.</summary>
    public IEnumerable<FonteDeCamada> ComAsReservas()
    {
        for (var fonte = this; fonte is not null; fonte = fonte.Reserva)
        {
            yield return fonte;
        }
    }

    /// <summary>URL com as marcações de data resolvidas.</summary>
    public string UrlResolvida() =>
        Url.Replace("{corte12m}", DateTime.UtcNow.AddMonths(-12).ToString("yyyy-MM-dd"));
}

/// <summary>
/// De onde cada camada vem e de quanto em quanto tempo precisa ser rebaixada.
/// </summary>
/// <remarks>
/// Este catálogo é o que torna a recarga desassistida possível. Antes dele, a
/// origem de cada camada existia apenas no runbook — e um procedimento escrito
/// só roda quando alguém lembra de rodá-lo. A diferença prática aparece em
/// dezembro: sem recarga, a base de embargos é a de hoje, e todo termo lavrado
/// nesse meio-tempo passa despercebido. O laudo continuaria dizendo "nenhuma
/// sobreposição" — com confiança, e errado.
///
/// Cada URL aqui foi confirmada contra a origem; as que exigem parâmetro
/// aparentemente supérfluo têm o motivo anotado, porque removê-los quebra o
/// download de um jeito que não parece causado por isso.
/// </remarks>
public static class CatalogoDeFontes
{
    private const int Semanal = 7;
    private const int Trimestral = 90;
    private const int Semestral = 180;
    private const int Anual = 365;

    /// <summary>Chave da camada de limites de biomas.</summary>
    /// <remarks>
    /// Ela é pré-requisito das camadas regionais: sem os limites carregados,
    /// nenhuma delas tem como declarar a região que cobre — e uma camada
    /// regional sem abrangência declarada é exatamente o defeito que a
    /// abrangência existe para fechar.
    /// </remarks>
    public const string BiomasDoBrasil = "biomas-brasil";

    public const string Amazonia = "Amazônia";
    public const string Caatinga = "Caatinga";
    public const string Cerrado = "Cerrado";
    public const string MataAtlantica = "Mata Atlântica";
    public const string Pampa = "Pampa";
    public const string Pantanal = "Pantanal";

    /// <summary>
    /// Primeiro ano que o desmatamento consolidado precisa alcançar.
    /// </summary>
    /// <remarks>
    /// É o corte dos compromissos de cadeia produtiva, e o mesmo que a regra
    /// DES-001 declara no nome. Antes a camada trazia um único ano e a regra
    /// dizia examinar desde 2008 — dezesseis anos que ela não tinha como
    /// encontrar, e ninguém ficava sabendo.
    /// </remarks>
    public const int CorteDoDesmatamento = 2008;

    /// <summary>
    /// Uma entrada do PRODES por bioma, todas a partir do corte.
    /// </summary>
    /// <remarks>
    /// Seis entradas, e não uma: o INPE publica um conjunto por bioma, com
    /// nome de camada próprio. Carregar só a Amazônia — que foi o que havia —
    /// deixa Goiás e Mato Grosso do Sul, juntos 334 mil imóveis da base, sem
    /// camada nenhuma de desmatamento consolidado.
    ///
    /// A Amazônia tem nome de camada diferente dos outros cinco, por isso o
    /// parâmetro; o resto do endereço é igual.
    /// </remarks>
    /// <summary>
    /// Quantas feições cabem numa requisição ao GeoServer do INPE.
    /// </summary>
    /// <remarks>
    /// É o <c>CountDefault</c> que ele declara no GetCapabilities. Pedir mais
    /// não dá erro — dá silêncio, que é pior.
    /// </remarks>
    public const int PorPagina = 50_000;

    private static FonteDeCamada Prodes(string bioma, string chave, string camadaWfs) =>
        new($"prodes-{chave}",
            $"PRODES {bioma}",
            TipoCamada.DesmatamentoConsolidado,
            $"INPE/TerraBrasilis ({camadaWfs})",
            FormatoDaFonte.ShapefileEmZip,
            Anual,
            "https://terrabrasilis.dpi.inpe.br/geoserver/ows?service=WFS&version=1.0.0" +
            $"&request=GetFeature&typeName={camadaWfs}" +
            $"&outputFormat=SHAPE-ZIP&CQL_FILTER=year%20%3E%3D%20{CorteDoDesmatamento}",
            Biomas: new[] { bioma },

            // fid, e não gid: é o identificador que esta camada publica, e a
            // paginação precisa de uma ordem estável para não repetir nem pular
            // feição entre páginas.
            ChaveDeOrdenacao: "fid");

    public static IReadOnlyList<FonteDeCamada> Todas { get; } = new[]
    {
        // O IBAMA publica diariamente. É a camada que mais envelhece: um termo
        // de embargo novo é exatamente o tipo de fato que a análise existe para
        // encontrar.
        new FonteDeCamada(
            EmbargoIbamaImporter.Chave,
            "Termos de embargo",
            TipoCamada.EmbargoAmbiental,
            "IBAMA — Dados Abertos (termo_de_embargo)",
            FormatoDaFonte.CsvEmbargo,
            Semanal,
            "https://stibamadadosabertosprd.blob.core.windows.net/dados-abertos/dados/" +
            "TERMOS_DE_EMBARGO/TERMO_EMBARGO/termo_de_embargo.csv"),

        // maxFeatures é obrigatório: sem ele o nginx da FUNAI devolve 403. O
        // valor cobre a base inteira, que tem 665 polígonos.
        new FonteDeCamada(
            "terra-indigena-funai",
            "Terras Indígenas",
            TipoCamada.TerraIndigena,
            "FUNAI — Geoserver (tis_poligonais)",
            FormatoDaFonte.ShapefileEmZip,
            Trimestral,
            "https://geoserver.funai.gov.br/geoserver/Funai/ows?service=WFS&version=1.0.0" +
            "&request=GetFeature&typeName=Funai:tis_poligonais&maxFeatures=5000" +
            "&outputFormat=SHAPE-ZIP",

            // O IBGE como reserva, porque a FUNAI é intermitente: em 01/10/2026
            // todo GetFeature dela passou a devolver 403 enquanto o
            // GetCapabilities do mesmo servidor seguia respondendo 200.
            //
            // A reserva cobre menos — 573 polígonos contra 665, porque o quadro
            // geográfico do IBGE não traz as terras em estudo. Mas camada
            // desatualizada não avisa que está desatualizada: ela só deixa de
            // encontrar o que passou a existir. Entre uma base um pouco menor e
            // uma base de setembro, a menor protege mais.
            Reserva: new FonteDeCamada(
                "terra-indigena-funai",
                "Terras Indígenas",
                TipoCamada.TerraIndigena,
                "IBGE — Terras Indígenas 2022 (reserva da FUNAI)",
                FormatoDaFonte.ShapefileEmZip,
                Trimestral,
                "https://geoservicos.ibge.gov.br/geoserver/CGMAT/ows?service=WFS&version=1.0.0" +
                "&request=GetFeature&typeName=CGMAT:qg_2022_610_terraindigena__v02" +
                "&outputFormat=SHAPE-ZIP",
                AnoReferencia: 2022)),

        // O filtro por data é necessário: a camada completa acumula anos de
        // alertas e a requisição sem filtro estoura em 504.
        //
        // Aqui os doze meses são legítimos, e não o recorte que escondia
        // desmatamento antigo: o DETER é aviso de desmatamento em curso, e o
        // histórico de quem já desmatou vem do PRODES — que agora carrega de
        // 2008 em diante. Sem essa contrapartida a janela era venda a
        // descoberto, e foi o que aconteceu enquanto o PRODES tinha só 2024.
        new FonteDeCamada(
            "deter-amazonia",
            "DETER Amazônia (12 meses)",
            TipoCamada.AlertaDesmatamento,
            "INPE/TerraBrasilis (deter_amz)",
            FormatoDaFonte.ShapefileEmZip,
            Semanal,
            "https://terrabrasilis.dpi.inpe.br/geoserver/deter-amz/ows?service=WFS" +
            "&version=1.0.0&request=GetFeature&typeName=deter-amz:deter_amz" +
            "&outputFormat=SHAPE-ZIP&CQL_FILTER=view_date%20%3E%3D%20'{corte12m}'",
            Biomas: new[] { Amazonia }),

        // O DETER do Cerrado existe desde sempre e nunca tinha sido carregado.
        // Goiás, Tocantins e Mato Grosso do Sul não têm outro aviso de
        // desmatamento em curso além deste.
        new FonteDeCamada(
            "deter-cerrado",
            "DETER Cerrado (12 meses)",
            TipoCamada.AlertaDesmatamento,
            "INPE/TerraBrasilis (deter_cerrado)",
            FormatoDaFonte.ShapefileEmZip,
            Semanal,
            "https://terrabrasilis.dpi.inpe.br/geoserver/ows?service=WFS" +
            "&version=1.0.0&request=GetFeature&typeName=deter-cerrado-nb:deter_cerrado" +
            "&outputFormat=SHAPE-ZIP&CQL_FILTER=view_date%20%3E%3D%20'{corte12m}'",
            Biomas: new[] { Cerrado }),

        // Os limites dos seis biomas, publicados pelo mesmo INPE que publica o
        // PRODES. É a camada que permite às outras declararem o que cobrem, e
        // por isso é carregada antes de qualquer camada regional.
        //
        // Vem do INPE, e não do IBGE que também a publica, por um motivo
        // prático: assim o recorte usado para dizer "o PRODES do Cerrado cobre
        // aqui" é exatamente o recorte com que o PRODES do Cerrado foi gerado.
        // Duas fontes quase iguais deixariam uma faixa de desacordo na divisa,
        // e divisa de bioma é onde ficam Goiás, Tocantins e Mato Grosso.
        new FonteDeCamada(
            BiomasDoBrasil,
            "Biomas do Brasil",
            TipoCamada.Bioma,
            "INPE/TerraBrasilis (biomas_brasil)",
            FormatoDaFonte.ShapefileEmZip,
            Anual,
            "https://terrabrasilis.dpi.inpe.br/geoserver/ows?service=WFS&version=1.0.0" +
            "&request=GetFeature&typeName=prodes-brasil-nb:biomas_brasil" +
            "&outputFormat=SHAPE-ZIP"),

        Prodes(Amazonia, "amazonia", "prodes-amazon-nb:yearly_deforestation_biome"),
        Prodes(Cerrado, "cerrado", "prodes-cerrado-nb:yearly_deforestation"),
        Prodes(Caatinga, "caatinga", "prodes-caatinga-nb:yearly_deforestation"),
        Prodes(MataAtlantica, "mata-atlantica", "prodes-mata-atlantica-nb:yearly_deforestation"),
        Prodes(Pampa, "pampa", "prodes-pampa-nb:yearly_deforestation"),
        Prodes(Pantanal, "pantanal", "prodes-pantanal-nb:yearly_deforestation"),

        // O CNUC não publica shapefile: o portal é uma aplicação JavaScript e o
        // download sai do backend em GeoJSON. O campo "format" é obrigatório —
        // sem ele o backend devolve 500 com erro do ogr2ogr.
        new FonteDeCamada(
            "unidade-conservacao-cnuc",
            "Unidades de Conservação",
            TipoCamada.UnidadeConservacao,
            "MMA — CNUC (ucs_selected)",
            FormatoDaFonte.GeoJsonEmZip,
            Trimestral,
            "https://cnuc-backend.mma.gov.br/api/v1/downloadGeo",
            CorpoJson:
                """
                {"map":"/var/www/storage/app/mapfiles/ucs.map","name":"ucs",
                 "typename":"ucs_selected","ucIds":"null","format":"GeoJSON"}
                """),

        new FonteDeCamada(
            "assentamento-incra",
            "Projetos de Assentamento",
            TipoCamada.AssentamentoRural,
            "INCRA — Acervo Fundiário (Assentamento Brasil)",
            FormatoDaFonte.ShapefileEmZip,
            Trimestral,
            "https://certificacao.incra.gov.br/csv_shp/zip/Assentamento%20Brasil.zip"),

        // Complementa o DETER em vez de repeti-lo: o DETER cobre Amazônia e
        // Cerrado, o MapBiomas Alerta cobre todos os biomas e já vem validado
        // contra imagem de alta resolução. Quando os dois apontam a mesma área,
        // o motor agrupa os achados sob a regra ALE-001 e o laudo cita as duas
        // origens — ninguém é bloqueado duas vezes pelo mesmo fato.
        //
        // Histórico inteiro, e não doze meses. A janela móvel era o defeito
        // mais caro do catálogo, e o que a denunciou foi um imóvel de Amaralina
        // em Goiás: três alertas de 2019 e 2020 dentro do perímetro, 17,49 ha,
        // e a análise dizendo que não havia sobreposição de desmatamento.
        //
        // Doze meses responde "foi desmatado recentemente?". Quem compra boi
        // pergunta outra coisa: "esta terra foi desmatada depois do corte de
        // 2008?" — e para essa pergunta um alerta de 2019 vale tanto quanto o
        // de ontem. Numa amostra de 36 imóveis dos seis estados da base, 12
        // tinham desmatamento real dentro do perímetro e o sistema enxergava 2.
        new FonteDeCamada(
            "mapbiomas-alerta",
            "MapBiomas Alerta",
            TipoCamada.AlertaDesmatamento,
            "MapBiomas Alerta (dashboard-alert-shapefile)",
            FormatoDaFonte.ShapefileEmZip,
            Semanal,
            "https://geoserver.alerta.mapbiomas.org/geoserver/ows?service=WFS&version=1.0.0" +
            "&request=GetFeature&typeName=mapbiomas-alertas:dashboard-alert-shapefile" +
            $"&outputFormat=SHAPE-ZIP&CQL_FILTER=detected_at%20%3E%3D%20'{CorteDoDesmatamento}-01-01'"),

        new FonteDeCamada(
            "quilombola-ibge",
            "Territórios Quilombolas",
            TipoCamada.TerritorioQuilombola,
            "IBGE — Território Quilombola 2022",
            FormatoDaFonte.ShapefileEmZip,
            Semestral,
            "https://geoservicos.ibge.gov.br/geoserver/CGMAT/ows?service=WFS&version=1.0.0" +
            "&request=GetFeature&typeName=CGMAT:qg_2022_620_territorioquilombola__v02" +
            "&outputFormat=SHAPE-ZIP",
            AnoReferencia: 2022)
    };

    public static FonteDeCamada? PorChave(string chave) =>
        Todas.FirstOrDefault(x => string.Equals(x.Chave, chave, StringComparison.OrdinalIgnoreCase));
}
