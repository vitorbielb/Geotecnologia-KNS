using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Ingestao;
using GeotecnologiaKNS.Geo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GeotecnologiaKNS.Geo;

public static class GeoDiExtension
{
    /// <summary>
    /// Registra o acesso às bases geoespaciais de referência (PostGIS).
    /// Sem connection string configurada, nada é registrado e a aplicação segue
    /// funcionando com o cadastro manual — a fase 1 é aditiva de propósito.
    /// </summary>
    public static IServiceCollection AddGeo(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddScoped<ICarLookupService, CarLookupIndisponivel>();
            services.AddScoped<IIntersecaoService, IntersecaoIndisponivel>();
            return services;
        }

        services.AddDbContext<GeoDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", GeoDbContext.Schema);
            }));

        services.AddScoped<ICarLookupService, CarLookupService>();
        services.AddScoped<IIntersecaoService, IntersecaoService>();
        services.AddScoped<SicarShapefileImporter>();
        services.AddScoped<CamadaShapefileImporter>();
        services.AddScoped<EmbargoIbamaImporter>();
        services.AddScoped<CamadaGeoJsonImporter>();
        services.AddScoped<CadastroEmpregadoresImporter>();
        services.AddScoped<IRestricaoDocumentoService, RestricaoDocumentoService>();

        return services;
    }
}

/// <summary>
/// Implementação nula usada quando o PostGIS ainda não foi configurado.
/// Falha explicitamente em vez de devolver "nenhuma sobreposição", que seria
/// lido como imóvel limpo.
/// </summary>
internal sealed class IntersecaoIndisponivel : IIntersecaoService
{
    private const string Mensagem =
        "As bases geoespaciais não estão configuradas; a análise automática não pode ser executada.";

    public Task<ResultadoCruzamento> CruzarPorCarAsync(string codigoCar, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Mensagem);

    public Task<IReadOnlyList<CamadaReferencia>> ObterCamadasAtivasAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CamadaReferencia>>(Array.Empty<CamadaReferencia>());
}

/// <summary>
/// Implementação nula usada quando o PostGIS ainda não foi configurado.
/// </summary>
internal sealed class CarLookupIndisponivel : ICarLookupService
{
    public Task<ImovelCarDto?> ObterPorCodigoAsync(string codigoCar, CancellationToken cancellationToken = default)
        => Task.FromResult<ImovelCarDto?>(null);

    public bool EstaConfigurado => false;

    public Task<bool> BaseDisponivelAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> MunicipioCobertoAsync(string codigoIbge, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task RegistrarLacunaAsync(string codigoIbge, string uf, string codigoCar, int tenantId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
