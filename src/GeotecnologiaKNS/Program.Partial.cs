namespace GeotecnologiaKNS
{
    public partial class Program
    {
    }

    public static class ProgramExtensions
    {
        public static async Task UpdateDatabaseAsync(this IApplicationBuilder app)
        {
            var serviceProvider = app.ApplicationServices;
            using var scope = serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.UpdateDatabaseAsync();
        }

        private static async Task UpdateDatabaseAsync(this ApplicationDbContext context)
        {
            if (context.Database.GetPendingMigrations().Any())
            {
                await context.Database.MigrateAsync();
            }
        }
    }
}
