using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for core configuration services registration.
/// Handles application-wide configuration and dependent service management.
/// </summary>
public static class CoreConfigurationExtensions
{
    /// <summary>
    /// Adds core configuration options and dependent service manager.
    /// </summary>
    public static IServiceCollection AddCoreConfiguration(
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

        // Dependent services manager (manages TextToSpeech.Service lifecycle)
        services.AddSingleton<IDependentServiceManager, DependentServicesManager>();

        return services;
    }
}
