using VirtualAssistant.Data;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub;
using VirtualAssistant.Core;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for data layer services registration.
/// Handles EF Core, CQRS handlers, and repository registration.
/// </summary>
public static class DataServicesExtensions
{
    /// <summary>
    /// Adds data layer services (EF Core, CQRS handlers, repositories).
    /// </summary>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core DbContext
        var connectionString = configuration.GetConnectionString("VirtualAssistantDb")
            ?? throw new InvalidOperationException("Connection string 'VirtualAssistantDb' not found.");
        services.AddVirtualAssistantData(connectionString);

        // Whisper transcription and LLM correction repositories
        services.AddScoped<IWhisperTranscriptionRepository, WhisperTranscriptionRepository>();
        services.AddScoped<ILlmCorrectionRepository, LlmCorrectionRepository>();

        // Transcription corrections repository (used by DatabaseCorrectionFilterStrategy)
        services.AddScoped<ITranscriptionCorrectionRepository, TranscriptionCorrectionRepository>();

        // GitHub sync services
        services.AddGitHubServices(configuration);

        // Core services (AgentHubService)
        services.AddCoreServices();

        return services;
    }
}
