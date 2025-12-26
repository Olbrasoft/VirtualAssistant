using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for core configuration services.
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Adds core configuration options.
    /// </summary>
    public static IServiceCollection AddCoreConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ContinuousListenerOptions>(
            configuration.GetSection(ContinuousListenerOptions.SectionName));

        services.Configure<ClaudeDispatchOptions>(
            configuration.GetSection(ClaudeDispatchOptions.SectionName));

        services.Configure<ExternalServicesOptions>(
            configuration.GetSection(ExternalServicesOptions.SectionName));

        return services;
    }
}
