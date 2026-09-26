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
            return services;
        }

        services.AddDbContext<GeoDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", GeoDbContext.Schema);
            }));

        services.AddScoped<ICarLookupService, CarLookupService>();
        services.AddScoped<SicarShapefileImporter>();

        return services;
    }
}

/// <summary>
/// Implementação nula usada quando o PostGIS ainda não foi configurado.
/// </summary>
internal sealed class CarLookupIndisponivel : ICarLookupService
{
    public Task<ImovelCarDto?> ObterPorCodigoAsync(string codigoCar, CancellationToken cancellationToken = default)
        => Task.FromResult<ImovelCarDto?>(null);

    public Task<bool> BaseDisponivelAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
