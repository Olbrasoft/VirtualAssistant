using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace VirtualAssistant.Data.EntityFrameworkCore;

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

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=virtual_assistant;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<VirtualAssistantDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.UseVector();
        });

        return new VirtualAssistantDbContext(optionsBuilder.Options);
    }
}
