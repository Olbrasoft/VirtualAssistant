using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.EntityFrameworkCore;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for WebApplication configuration.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies pending database migrations automatically.
    /// </summary>
    public static WebApplication ApplyDatabaseMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Count > 0)
            {
                logger.LogInformation("Applying {Count} pending database migrations: {Migrations}",
                    pendingMigrations.Count, string.Join(", ", pendingMigrations));
            }

            dbContext.Database.Migrate();

            if (pendingMigrations.Count > 0)
            {
                logger.LogInformation("Database migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }

        return app;
    }
}
