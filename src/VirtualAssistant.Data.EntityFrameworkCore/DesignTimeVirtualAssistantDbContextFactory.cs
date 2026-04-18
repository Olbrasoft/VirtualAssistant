using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Factory for creating VirtualAssistantDbContext at design time (for EF Core migrations).
/// </summary>
public class DesignTimeVirtualAssistantDbContextFactory : IDesignTimeDbContextFactory<VirtualAssistantDbContext>
{
    public VirtualAssistantDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Design-time factory used only for EF Core migrations. Refuse to run
        // with a hardcoded fallback (#984) — developers must supply the
        // connection string via either the VIRTUAL_ASSISTANT_CONNECTION
        // environment variable or the "DefaultConnection" value in
        // appsettings[.Development].json. This prevents accidental migrations
        // against a shared localhost DB with a stock Username/Password.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("VIRTUAL_ASSISTANT_CONNECTION")
            ?? throw new InvalidOperationException(
                "No database connection string found. Set ConnectionStrings:DefaultConnection in " +
                "appsettings[.Development].json or the VIRTUAL_ASSISTANT_CONNECTION environment variable.");

        var optionsBuilder = new DbContextOptionsBuilder<VirtualAssistantDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.UseVector();
        });

        return new VirtualAssistantDbContext(optionsBuilder.Options);
    }
}
