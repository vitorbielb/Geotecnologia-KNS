using System.Globalization;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GeotecnologiaKNS.Geo.Services;

/// <summary>
/// Uma sobreposição encontrada entre o imóvel e uma camada de referência.
/// </summary>
public record Sobreposicao(
    string CamadaChave,
    string CamadaNome,
    TipoCamada Tipo,
    string Origem,
    int? AnoReferencia,
    string? Rotulo,
    string? AtributosJson,
    double AreaSobrepostaHa,
    double PercentualDoImovel,

    /// <summary>
    /// A área de interseção em GeoJSON, simplificada, para o laudo desenhar.
    /// </summary>
    /// <remarks>
    /// Vem simplificada e só quando pedida. Um imóvel que toca dezenas de
    /// polígonos do PRODES traria megabytes de geometria para uma imagem de
    /// 640 pixels, e a tolerância de 0,0005 grau — cerca de 50 metros — é
    /// invisível nessa escala.
    /// </remarks>
    string? RecorteGeoJson = null);

/// <param name="TiposVerificados">
/// Tipos de camada que cobrem <b>este</b> imóvel e foram de fato cruzados.
/// Ausência de sobreposição só significa "nada encontrado" para um tipo que
/// está nesta lista; para os demais, significa que não houve o que consultar.
/// </param>
/// <param name="CobreDesdeAno">
/// Primeiro ano que cada tipo alcança sobre este imóvel. Ausente da lista, ou
/// com valor nulo, significa que o tipo não tem recorte temporal.
/// </param>
/// <param name="TiposExistentes">
/// Tipos com camada carregada em algum lugar do país, cubram este imóvel ou
/// não. Serve para separar duas faltas que exigem providências diferentes:
/// nenhuma camada do tipo foi carregada, ou foram — e nenhuma delas alcança
/// esta região.
/// </param>
/// <remarks>
/// Os dois campos respondem à mesma pergunta em eixos diferentes: até onde a
/// verificação foi, no espaço e no tempo. Faltava o segundo, e a falta era
/// visível — a regra DES-001 se chama "desmatamento consolidado a partir de
/// 2008" e era avaliada contra uma camada que só tinha o ano de 2024.
/// </remarks>
public record ResultadoCruzamento(
    string CodigoCar,
    double AreaImovelHa,
    IReadOnlyList<Sobreposicao> Sobreposicoes,
    DateTime ExecutadoEm,
    IReadOnlyList<TipoCamada> TiposVerificados,
    IReadOnlyDictionary<TipoCamada, int?>? CobreDesdeAno = null,
    IReadOnlyList<TipoCamada>? TiposExistentes = null);

/// <summary>
/// Uma camada ativa e o alcance dela sobre o imóvel analisado.
/// </summary>
/// <param name="CobreOImovel">
/// Falso quando a camada existe mas a região dela não inclui este imóvel. O
/// laudo precisa citá-la assim mesmo, e dizendo isso: omiti-la faria parecer
/// que a camada não existe, e listá-la sem a ressalva faria parecer que ela
/// respondeu.
/// </param>
public record CamadaConsultada(CamadaReferencia Camada, bool CobreOImovel);

public interface IIntersecaoService
{
    /// <summary>
    /// Cruza o perímetro do imóvel contra as camadas públicas e as da indústria.
    /// </summary>
    /// <param name="tenantId">
    /// Indústria em nome de quem a análise roda, ou nulo para usar só as
    /// camadas públicas. É obrigatório declarar: as indústrias atendidas são
    /// concorrentes entre si, e um parâmetro opcional transformaria o
    /// esquecimento de quem chama em vazamento silencioso.
    /// </param>
    Task<ResultadoCruzamento> CruzarPorCarAsync(
        string codigoCar, int? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// As áreas de sobreposição, em GeoJSON simplificado, para desenhar no laudo.
    /// </summary>
    Task<IReadOnlyList<Sobreposicao>> ObterRecortesAsync(
        string codigoCar, int? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Camadas ativas e se cada uma alcança este imóvel, para o laudo listar o
    /// que foi de fato consultado.
    /// </summary>
    /// <remarks>
    /// O código do imóvel é obrigatório, e essa é a correção. A lista saía sem
    /// ele, igual para o país inteiro, e por isso o laudo de uma fazenda de
    /// Goiás trazia "PRODES — 48.750 feições" entre as bases consultadas: uma
    /// camada que não tem um polígono sequer no estado, citada como se
    /// tivesse respondido alguma coisa.
    /// </remarks>
    Task<IReadOnlyList<CamadaConsultada>> ObterCamadasAtivasAsync(
        string codigoCar, int? tenantId, CancellationToken cancellationToken = default);
}

public class IntersecaoService : IIntersecaoService
{
    private readonly GeoDbContext _context;

    public IntersecaoService(GeoDbContext context)
    {
        _context = context;
    }

    public async Task<ResultadoCruzamento> CruzarPorCarAsync(
        string codigoCar, int? tenantId, CancellationToken cancellationToken = default)
    {
        var normalizado = CodigoCar.Normalizar(codigoCar)
            ?? throw new ArgumentException("Código do CAR inválido.", nameof(codigoCar));

        var areaImovel = await ObterAreaImovelHaAsync(normalizado, cancellationToken);

        if (areaImovel is null)
        {
            throw new InvalidOperationException($"Imóvel {normalizado} não está na base do CAR.");
        }

        var sobreposicoes = await ConsultarSobreposicoesAsync(
            normalizado, areaImovel.Value, tenantId, cancellationToken);

        var camadas = await ConsultarCoberturaAsync(normalizado, tenantId, cancellationToken);

        var cobrem = camadas.Where(x => x.CobreOImovel).ToList();

        return new ResultadoCruzamento(
            normalizado,
            areaImovel.Value,
            sobreposicoes,
            DateTime.UtcNow,
            cobrem.Select(x => x.Tipo).Distinct().ToList(),

            // Basta uma camada do tipo sem recorte temporal para o tipo inteiro
            // não ter recorte. Por isso o Any vem antes do Min: o Min sozinho
            // ignoraria o nulo e responderia o ano da camada mais limitada, que
            // é o oposto da verdade.
            cobrem.GroupBy(x => x.Tipo).ToDictionary(
                g => g.Key,
                g => g.Any(x => x.DesdeAno is null) ? null : g.Min(x => x.DesdeAno)),

            camadas.Select(x => x.Tipo).Distinct().ToList());
    }

    public async Task<IReadOnlyList<CamadaConsultada>> ObterCamadasAtivasAsync(
        string codigoCar, int? tenantId, CancellationToken cancellationToken = default)
    {
        var normalizado = CodigoCar.Normalizar(codigoCar);

        var camadas = await CamadasVisiveis(tenantId)
            .OrderBy(x => x.Tipo)
            .ThenBy(x => x.Nome)
            .ToListAsync(cancellationToken);

        if (normalizado is null)
        {
            return camadas.Select(x => new CamadaConsultada(x, true)).ToList();
        }

        // A abrangência é comparada no banco porque é lá que está o perímetro;
        // trazer os dois para a memória para intersectar seria carregar
        // geometria de bioma inteiro a cada análise.
        const string Sql = @"
            SELECT c.id
            FROM geo.camada_referencia c
            JOIN geo.imovel_car i ON i.codigo_car = @codigo
            WHERE c.abrangencia IS NULL OR ST_Intersects(c.abrangencia, i.perimetro)";

        await using var comando = await CriarComandoAsync(Sql, cancellationToken);
        comando.Parameters.AddWithValue("codigo", normalizado);

        var alcancam = new HashSet<int>();

        await using (var leitor = await comando.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                alcancam.Add(leitor.GetInt32(0));
            }
        }

        return camadas.Select(x => new CamadaConsultada(x, alcancam.Contains(x.Id))).ToList();
    }

    /// <summary>
    /// Os limites de biomas existem para recortar a abrangência das outras
    /// camadas, e não para restringir imóvel nenhum.
    /// </summary>
    /// <remarks>
    /// Sem esta exclusão todo imóvel do país passaria a ter uma "sobreposição"
    /// com o próprio bioma — do tamanho do imóvel inteiro. Nenhuma regra
    /// examina o tipo Bioma, então veredito nenhum mudaria; o mapa do laudo,
    /// sim: ele desenha o que o cruzamento devolve, e pintaria a fazenda
    /// inteira de cor de alerta.
    /// </remarks>
    private const int TipoBioma = (int)TipoCamada.Bioma;

    /// <summary>
    /// Camadas públicas mais as da própria indústria — nunca as de outra.
    /// </summary>
    private IQueryable<CamadaReferencia> CamadasVisiveis(int? tenantId) =>
        _context.Camadas
            .AsNoTracking()
            .Where(x => x.Ativa
                        && x.Tipo != TipoCamada.Bioma
                        && (x.TenantId == null || x.TenantId == tenantId));

    /// <summary>
    /// Área do imóvel em hectares, calculada sobre a geometria.
    /// </summary>
    /// <remarks>
    /// O cast para geography é o que dá área em metros quadrados reais;
    /// ST_Area sobre geometry em SRID 4326 devolveria graus quadrados, que não
    /// significam nada.
    /// </remarks>
    private async Task<double?> ObterAreaImovelHaAsync(string codigoCar, CancellationToken cancellationToken)
    {
        const string Sql = @"
            SELECT ST_Area(perimetro::geography) / 10000.0
            FROM geo.imovel_car
            WHERE codigo_car = @codigo";

        await using var comando = await CriarComandoAsync(Sql, cancellationToken);
        comando.Parameters.AddWithValue("codigo", codigoCar);

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);

        return resultado is null or DBNull ? null : Convert.ToDouble(resultado, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tipos de camada que cobrem este imóvel, e desde quando cada um alcança.
    /// </summary>
    /// <remarks>
    /// O que foi consultado importa tanto quanto o que foi encontrado: sem esta
    /// lista, "nenhuma sobreposição" fica indistinguível de "não havia base
    /// para consultar".
    ///
    /// A pergunta é sobre <b>este</b> imóvel, e essa é a correção. Antes ela
    /// era "existe alguma camada deste tipo?", respondida uma vez para o país
    /// inteiro — e o PRODES da Amazônia respondia que sim para um imóvel de
    /// Goiás, onde não tem um polígono sequer. A regra se dizia avaliada, o
    /// laudo listava a camada entre as verificadas, e o imóvel saía liberado
    /// por uma verificação que nunca houve. São 334 mil imóveis da base em
    /// Goiás e Mato Grosso do Sul, todos fora do alcance das camadas de
    /// desmatamento que existiam.
    ///
    /// Camada sem feição publicada não conta: uma primeira carga recusada deixa
    /// a camada cadastrada e vazia, e ela se diria verificada do mesmo jeito.
    /// </remarks>
    private sealed record CamadaNoImovel(TipoCamada Tipo, bool CobreOImovel, int? DesdeAno);

    private async Task<List<CamadaNoImovel>> ConsultarCoberturaAsync(
        string codigoCar, int? tenantId, CancellationToken cancellationToken)
    {
        // Uma linha por camada ativa, dizendo se ela alcança este imóvel. A
        // junção por tipo fica em memória porque são dezenas de camadas, não
        // milhões — e aqui a clareza vale mais que a agregação no banco.
        //
        // Abrangência nula é cobertura nacional: o caso das camadas que de fato
        // cobrem o país, como os embargos do IBAMA.
        const string Sql = @"
            SELECT c.tipo,
                   (c.abrangencia IS NULL OR ST_Intersects(c.abrangencia, i.perimetro)) AS cobre,
                   c.cobre_desde_ano AS desde
            FROM geo.camada_referencia c
            JOIN geo.imovel_car i ON i.codigo_car = @codigo
            WHERE c.ativa
              AND c.total_feicoes > 0
              AND c.tipo <> @bioma
              AND (c.tenant_id IS NULL OR c.tenant_id = @tenant)";

        await using var comando = await CriarComandoAsync(Sql, cancellationToken);
        comando.Parameters.AddWithValue("codigo", codigoCar);
        comando.Parameters.AddWithValue("tenant", (object?)tenantId ?? DBNull.Value);
        comando.Parameters.AddWithValue("bioma", TipoBioma);

        var camadas = new List<CamadaNoImovel>();

        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            camadas.Add(new CamadaNoImovel(
                (TipoCamada)leitor.GetInt32(0),
                leitor.GetBoolean(1),
                leitor.IsDBNull(2) ? null : leitor.GetInt32(2)));
        }

        return camadas;
    }

    private async Task<List<Sobreposicao>> ConsultarSobreposicoesAsync(
        string codigoCar,
        double areaImovelHa,
        int? tenantId,
        CancellationToken cancellationToken)
    {
        // ST_Intersects usa o índice GiST para descartar o que não encosta;
        // ST_Intersection, que é caro, só roda no que sobrou. ST_IsValid protege
        // contra feições de origem com geometria quebrada, que abortariam a query.
        // O filtro por tenant está na mesma consulta que o cruzamento, e não
        // numa camada acima, porque é aqui que ele não tem como ser esquecido.
        const string Sql = @"
            SELECT c.chave,
                   c.nome,
                   c.tipo,
                   c.origem,
                   COALESCE(f.ano, c.ano_referencia) AS ano,
                   f.rotulo,
                   f.atributos::text,
                   ST_Area(ST_Intersection(f.geometria, i.perimetro)::geography) / 10000.0 AS area_ha
            FROM geo.imovel_car i
            JOIN geo.feicao_referencia f
              ON ST_Intersects(f.geometria, i.perimetro)
            JOIN geo.camada_referencia c
              ON c.id = f.camada_id
             AND f.versao = c.versao_atual
            WHERE i.codigo_car = @codigo
              AND c.ativa
              AND c.tipo <> @bioma
              AND (c.tenant_id IS NULL OR c.tenant_id = @tenant)
              AND ST_IsValid(f.geometria)
              AND ST_Area(ST_Intersection(f.geometria, i.perimetro)::geography) > 0
            ORDER BY area_ha DESC";

        await using var comando = await CriarComandoAsync(Sql, cancellationToken);
        comando.Parameters.AddWithValue("codigo", codigoCar);
        comando.Parameters.AddWithValue("tenant", (object?)tenantId ?? DBNull.Value);
        comando.Parameters.AddWithValue("bioma", TipoBioma);

        var sobreposicoes = new List<Sobreposicao>();

        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            var areaSobreposta = leitor.IsDBNull(7) ? 0 : leitor.GetDouble(7);

            sobreposicoes.Add(new Sobreposicao(
                CamadaChave: leitor.GetString(0),
                CamadaNome: leitor.GetString(1),
                Tipo: (TipoCamada)leitor.GetInt32(2),
                Origem: leitor.GetString(3),
                AnoReferencia: leitor.IsDBNull(4) ? null : leitor.GetInt32(4),
                Rotulo: leitor.IsDBNull(5) ? null : leitor.GetString(5),
                AtributosJson: leitor.IsDBNull(6) ? null : leitor.GetString(6),
                AreaSobrepostaHa: areaSobreposta,
                PercentualDoImovel: areaImovelHa > 0 ? areaSobreposta / areaImovelHa * 100 : 0));
        }

        return sobreposicoes;
    }

    /// <summary>
    /// Repete o cruzamento trazendo a geometria da interseção.
    /// </summary>
    /// <remarks>
    /// Consulta própria, e não um parâmetro na do laudo, porque a geometria é
    /// cara: ela só é buscada quando alguém pede o documento, e não em toda
    /// análise que entra na fila.
    /// </remarks>
    public async Task<IReadOnlyList<Sobreposicao>> ObterRecortesAsync(
        string codigoCar, int? tenantId, CancellationToken cancellationToken = default)
    {
        var normalizado = CodigoCar.Normalizar(codigoCar)
            ?? throw new ArgumentException("Código do CAR inválido.", nameof(codigoCar));

        var areaImovel = await ObterAreaImovelHaAsync(normalizado, cancellationToken) ?? 0;

        const string Sql = @"
            SELECT c.chave,
                   c.nome,
                   c.tipo,
                   c.origem,
                   COALESCE(f.ano, c.ano_referencia) AS ano,
                   f.rotulo,
                   ST_Area(ST_Intersection(f.geometria, i.perimetro)::geography) / 10000.0 AS area_ha,
                   ST_AsGeoJSON(
                       ST_SimplifyPreserveTopology(
                           ST_Intersection(f.geometria, i.perimetro), 0.0005), 6) AS recorte
            FROM geo.imovel_car i
            JOIN geo.feicao_referencia f
              ON ST_Intersects(f.geometria, i.perimetro)
            JOIN geo.camada_referencia c
              ON c.id = f.camada_id
             AND f.versao = c.versao_atual
            WHERE i.codigo_car = @codigo
              AND c.ativa
              AND c.tipo <> @bioma
              AND (c.tenant_id IS NULL OR c.tenant_id = @tenant)
              AND ST_IsValid(f.geometria)
              AND ST_Area(ST_Intersection(f.geometria, i.perimetro)::geography) > 0
            ORDER BY area_ha DESC";

        await using var comando = await CriarComandoAsync(Sql, cancellationToken);
        comando.Parameters.AddWithValue("codigo", normalizado);
        comando.Parameters.AddWithValue("tenant", (object?)tenantId ?? DBNull.Value);
        comando.Parameters.AddWithValue("bioma", TipoBioma);

        var recortes = new List<Sobreposicao>();

        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            var area = leitor.IsDBNull(6) ? 0 : leitor.GetDouble(6);

            recortes.Add(new Sobreposicao(
                CamadaChave: leitor.GetString(0),
                CamadaNome: leitor.GetString(1),
                Tipo: (TipoCamada)leitor.GetInt32(2),
                Origem: leitor.GetString(3),
                AnoReferencia: leitor.IsDBNull(4) ? null : leitor.GetInt32(4),
                Rotulo: leitor.IsDBNull(5) ? null : leitor.GetString(5),
                AtributosJson: null,
                AreaSobrepostaHa: area,
                PercentualDoImovel: areaImovel > 0 ? area / areaImovel * 100 : 0,
                RecorteGeoJson: leitor.IsDBNull(7) ? null : leitor.GetString(7)));
        }

        return recortes;
    }

    private async Task<NpgsqlCommand> CriarComandoAsync(string sql, CancellationToken cancellationToken)
    {
        var conexao = (NpgsqlConnection)_context.Database.GetDbConnection();

        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(cancellationToken);
        }

        var comando = conexao.CreateCommand();
        comando.CommandText = sql;

        // Cruzamento contra camadas nacionais é pesado; o padrão de 30s derruba
        // análises legítimas de imóveis grandes.
        comando.CommandTimeout = 180;

        return comando;
    }
}
