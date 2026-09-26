using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GeotecnologiaKNS.Geo;

/// <summary>
/// Usada apenas pelas ferramentas de linha de comando do EF Core (dotnet ef).
/// A connection string vem de GEO_CONNECTION_STRING quando definida; o valor
/// padrão aponta para o PostGIS do docker-compose.
/// </summary>
public class GeoDbContextFactory : IDesignTimeDbContextFactory<GeoDbContext>
{
    private const string ConexaoLocal =
        "Host=localhost;Port=5432;Database=geotecnologiakns_geo;Username=geo;Password=geo_local_dev";

    public GeoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GEO_CONNECTION_STRING") ?? ConexaoLocal;

        var options = new DbContextOptionsBuilder<GeoDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", GeoDbContext.Schema);
            })
            .Options;

        return new GeoDbContext(options);
    }
}
