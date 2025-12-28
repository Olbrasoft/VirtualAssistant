using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub;
using VirtualAssistant.Core;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering data layer services (EF Core, repositories).
/// </summary>
public static class DataServicesExtensions
{
    /// <summary>
    /// Adds data layer services including DbContext, repositories, and GitHub sync.
    /// </summary>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database connection
        var connectionString = configuration.GetConnectionString("VirtualAssistantDb")
            ?? throw new InvalidOperationException("Connection string 'VirtualAssistantDb' not found.");
        services.AddVirtualAssistantData(connectionString);

        // Whisper transcription and LLM correction repositories
        services.AddScoped<IWhisperTranscriptionRepository, WhisperTranscriptionRepository>();
        services.AddScoped<ILlmCorrectionRepository, LlmCorrectionRepository>();

        // GitHub sync services
        services.AddGitHubServices(configuration);

        // Core services (AgentHubService)
        services.AddCoreServices();

        return services;
    }
}
