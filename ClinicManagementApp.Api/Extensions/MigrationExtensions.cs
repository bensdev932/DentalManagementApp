using ClinicManagementApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Extensions;

/// <summary>
/// Extension methods for database schema initialization and migration execution.
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Safely applies any pending EF Core PostgreSQL migrations upon startup with resilient logging.
    /// </summary>
    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();

            if (pendingMigrations.Count > 0)
            {
                logger.LogInformation("Applying {Count} pending PostgreSQL migration(s)...", pendingMigrations.Count);
                dbContext.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("PostgreSQL database schema is up-to-date.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not automatically apply migrations at startup. Ensure PostgreSQL is running on the configured connection string.");
        }
    }
}

