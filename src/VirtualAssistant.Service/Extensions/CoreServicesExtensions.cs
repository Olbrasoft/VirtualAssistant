using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using VirtualAssistant.Core.Services;

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

        // Dependent services infrastructure (SRP-compliant components)
        // NOTE: These services are currently unused after removing DependentServicesManager
        // but kept for potential future use with other services
        services.AddSingleton<IPortChecker, PortChecker>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<ISystemdServiceStarter, SystemdServiceStarter>();
        services.AddSingleton<IProcessServiceStarter, ProcessServiceStarter>();
        services.AddSingleton<IServiceHealthMonitor, ServiceHealthMonitor>();
        services.AddSingleton<IServiceLifecycleManager, ServiceLifecycleManager>();

        return services;
    }
}
