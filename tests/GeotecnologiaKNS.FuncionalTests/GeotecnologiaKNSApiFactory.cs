using GeotecnologiaKNS.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GeotecnologiaKNS.FuncionalTests
{
    public class GeotecnologiaKNSApiFactory<T> : WebApplicationFactory<T>, IAsyncLifetime
        where T : class
    {
        public async Task InitializeAsync()
            => await EnsureDatabaseDeletedAsync();

        async Task IAsyncLifetime.DisposeAsync()
            => await EnsureDatabaseDeletedAsync();

        private async Task EnsureDatabaseDeletedAsync()
        {
            using var scope = Services.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }
}