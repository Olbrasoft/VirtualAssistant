using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Processes;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering core configuration and services.
/// </summary>
public static class CoreServicesExtensions
{
    /// <summary>
    /// Adds core configuration options and foundational services.
    /// </summary>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration options
        services.Configure<ContinuousListenerOptions>(
            configuration.GetSection(ContinuousListenerOptions.SectionName));

        services.Configure<DictationOptions>(
            configuration.GetSection(DictationOptions.SectionName));

        services.Configure<ClaudeDispatchOptions>(
            configuration.GetSection(ClaudeDispatchOptions.SectionName));

        services.Configure<ExternalServicesOptions>(
            configuration.GetSection(ExternalServicesOptions.SectionName));

        // NOTE: DependentServicesManager removed after inline TTS integration (issue #407)
        // TTS providers now run inline, no external service lifecycle management needed

        // Process executor for DIP compliance (issue #470)
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();

        // Settings service for persistent configuration
        services.AddSingleton<ISettingsService, SettingsService>();

        // Dictation persistence service (scoped - uses EF repositories)
        services.AddScoped<IDictationPersistenceService, DictationPersistenceService>();

        return services;
    }
}
