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
    FonteDeCamada? Reserva = null)
{
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
        // alertas e a requisição sem filtro estoura em 504. A janela móvel de
        // doze meses acompanha a recarga — é o que mantém o alerta "recente".
        new FonteDeCamada(
            "deter-amazonia",
            "DETER Amazônia (12 meses)",
            TipoCamada.AlertaDesmatamento,
            "INPE/TerraBrasilis (deter_amz)",
            FormatoDaFonte.ShapefileEmZip,
            Semanal,
            "https://terrabrasilis.dpi.inpe.br/geoserver/deter-amz/ows?service=WFS" +
            "&version=1.0.0&request=GetFeature&typeName=deter-amz:deter_amz" +
            "&outputFormat=SHAPE-ZIP&CQL_FILTER=view_date%20%3E%3D%20'{corte12m}'"),

        // Recarregar o PRODES de 2024 não traz 2025: traz a revisão de 2024,
        // que o INPE republica. Um ano novo é camada nova, com entrada própria
        // neste catálogo — e isso é de propósito, porque trocar o ano de
        // referência por baixo mudaria o significado dos laudos já emitidos.
        new FonteDeCamada(
            "prodes-amazonia-2024",
            "PRODES",
            TipoCamada.DesmatamentoConsolidado,
            "INPE/TerraBrasilis (yearly_deforestation_biome)",
            FormatoDaFonte.ShapefileEmZip,
            Anual,
            "https://terrabrasilis.dpi.inpe.br/geoserver/ows?service=WFS&version=1.0.0" +
            "&request=GetFeature&typeName=prodes-amazon-nb:yearly_deforestation_biome" +
            "&outputFormat=SHAPE-ZIP&CQL_FILTER=year%3D2024",
            AnoReferencia: 2024),

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

        // A fonte é o IBGE, não o INCRA: todas as rotas do INCRA exigem login.
        // Complementa o DETER em vez de repeti-lo: o DETER cobre Amazônia, o
        // MapBiomas Alerta cobre todos os biomas e já vem validado contra
        // imagem de alta resolução. Quando os dois apontam a mesma área, o
        // motor agrupa os dois achados sob a regra ALE-001 e o laudo cita as
        // duas origens — ninguém é bloqueado duas vezes pelo mesmo fato.
        new FonteDeCamada(
            "mapbiomas-alerta",
            "MapBiomas Alerta (12 meses)",
            TipoCamada.AlertaDesmatamento,
            "MapBiomas Alerta (dashboard-alert-shapefile)",
            FormatoDaFonte.ShapefileEmZip,
            Semanal,
            "https://geoserver.alerta.mapbiomas.org/geoserver/ows?service=WFS&version=1.0.0" +
            "&request=GetFeature&typeName=mapbiomas-alertas:dashboard-alert-shapefile" +
            "&outputFormat=SHAPE-ZIP&CQL_FILTER=detected_at%20%3E%3D%20'{corte12m}'"),

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
