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
    double PercentualDoImovel);

/// <param name="TiposVerificados">
/// Tipos de camada que existiam e foram de fato cruzados. Ausência de
/// sobreposição só significa "nada encontrado" para um tipo que está nesta
/// lista; para os demais, significa que não houve o que consultar.
/// </param>
public record ResultadoCruzamento(
    string CodigoCar,
    double AreaImovelHa,
    IReadOnlyList<Sobreposicao> Sobreposicoes,
    DateTime ExecutadoEm,
    IReadOnlyList<TipoCamada> TiposVerificados);

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

    /// <summary>Camadas ativas, para exibir no laudo o que foi de fato verificado.</summary>
    Task<IReadOnlyList<CamadaReferencia>> ObterCamadasAtivasAsync(
        int? tenantId, CancellationToken cancellationToken = default);
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

        // O que foi consultado importa tanto quanto o que foi encontrado: sem
        // esta lista, "nenhuma sobreposição" fica indistinguível de "não havia
        // base para consultar".
        //
        // Sem feição publicada a camada não conta. O caso é concreto: uma
        // primeira carga recusada deixa a camada cadastrada e vazia, e sem este
        // filtro o tipo dela entraria aqui — a regra se diria avaliada, e o
        // laudo liberaria o imóvel por omissão.
        var tiposVerificados = await CamadasVisiveis(tenantId)
            .Where(x => x.TotalFeicoes > 0)
            .Select(x => x.Tipo)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new ResultadoCruzamento(
            normalizado, areaImovel.Value, sobreposicoes, DateTime.UtcNow, tiposVerificados);
    }

    public async Task<IReadOnlyList<CamadaReferencia>> ObterCamadasAtivasAsync(
        int? tenantId, CancellationToken cancellationToken = default)
    {
        return await CamadasVisiveis(tenantId)
            .OrderBy(x => x.Tipo)
            .ThenBy(x => x.Nome)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Camadas públicas mais as da própria indústria — nunca as de outra.
    /// </summary>
    private IQueryable<CamadaReferencia> CamadasVisiveis(int? tenantId) =>
        _context.Camadas
            .AsNoTracking()
            .Where(x => x.Ativa && (x.TenantId == null || x.TenantId == tenantId));

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
                   c.ano_referencia,
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
              AND (c.tenant_id IS NULL OR c.tenant_id = @tenant)
              AND ST_IsValid(f.geometria)
              AND ST_Area(ST_Intersection(f.geometria, i.perimetro)::geography) > 0
            ORDER BY area_ha DESC";

        await using var comando = await CriarComandoAsync(Sql, cancellationToken);
        comando.Parameters.AddWithValue("codigo", codigoCar);
        comando.Parameters.AddWithValue("tenant", (object?)tenantId ?? DBNull.Value);

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
