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

// Optimization pass: 2025-04-07 20:30:19

// Optimization pass: 2025-04-11 06:40:41

// Optimization pass: 2025-04-14 12:21:13

// Optimization pass: 2025-04-16 21:05:20

// Optimization pass: 2025-04-30 18:28:56

// Optimization pass: 2025-05-06 12:56:47

// Optimization pass: 2025-05-13 16:00:55

// Optimization pass: 2025-05-15 21:28:25

// Optimization pass: 2025-05-21 16:29:52

// Optimization pass: 2025-05-23 08:14:58

// Optimization pass: 2025-08-04 17:03:10

// Optimization pass: 2025-08-07 11:08:47

// Optimization pass: 2025-08-13 21:03:49

// Optimization pass: 2025-08-14 16:58:10

// Optimization pass: 2025-08-19 02:55:46

// Optimization pass: 2025-08-20 12:01:38

// Optimization pass: 2025-08-22 17:47:02

// Optimization pass: 2025-08-26 09:13:47

// Optimization pass: 2025-09-04 11:58:59

// Optimization pass: 2025-09-05 16:27:21

// Optimization pass: 2025-09-09 19:35:01

// Optimization pass: 2025-09-12 20:40:59

// Optimization pass: 2025-09-17 08:31:18

// Optimization pass: 2025-09-22 19:58:35

// Optimization pass: 2025-09-25 09:37:24

// Optimization pass: 2025-09-29 04:56:20

// Optimization pass: 2025-10-01 00:40:45

// Optimization pass: 2025-10-01 21:54:56

// Optimization pass: 2025-10-10 05:27:10

// Optimization pass: 2025-10-13 07:06:33

// Optimization pass: 2025-10-15 22:46:17

// Optimization pass: 2025-10-17 02:19:01

// Optimization pass: 2025-10-20 07:14:19

// Optimization pass: 2025-10-24 07:06:18

// Optimization pass: 2025-10-30 08:16:57
