using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Background service that plays a startup notification when the application starts.
/// Phase 1: Simple static notification "Systém nastartován".
/// </summary>
public sealed class StartupNotificationService : IHostedService
{
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly ILogger<StartupNotificationService> _logger;

    public StartupNotificationService(IVirtualAssistantSpeaker speaker, ILogger<StartupNotificationService> logger)
    {
        _speaker = speaker;
        _logger = logger;
    }

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// Currently logs readiness message without playing audio (TTS disabled for startup).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop service startup.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // TTS disabled - notifications are stored in database, not spoken immediately
        _logger.LogInformation("Startup notification service ready (TTS disabled)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop service shutdown.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
