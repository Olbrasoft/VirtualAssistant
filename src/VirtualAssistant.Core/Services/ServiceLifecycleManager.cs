using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Manages service start and stop lifecycle.
/// </summary>
public class ServiceLifecycleManager : IServiceLifecycleManager
{
    /// <summary>
    /// Delay in milliseconds to allow service to start before health check.
    /// </summary>
    private const int SERVICE_STARTUP_DELAY_MS = 2000;

    private readonly ILogger<ServiceLifecycleManager> _logger;
    private readonly ISystemdServiceStarter _systemdStarter;
    private readonly IProcessServiceStarter _processStarter;
    private readonly IServiceHealthMonitor _healthMonitor;
    private readonly IProcessManager _processManager;

    public ServiceLifecycleManager(
        ILogger<ServiceLifecycleManager> logger,
        ISystemdServiceStarter systemdStarter,
        IProcessServiceStarter processStarter,
        IServiceHealthMonitor healthMonitor,
        IProcessManager processManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemdStarter = systemdStarter ?? throw new ArgumentNullException(nameof(systemdStarter));
        _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
    }

    /// <inheritdoc/>
    public async Task StartServiceAsync(DependentServiceInfo service, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting {ServiceName}...", service.Name);

        // Check if already running
        if (await _healthMonitor.CheckHealthAsync(service, cancellationToken))
        {
            _logger.LogInformation("{ServiceName} is already running", service.Name);
            service.IsRunning = true;
            return;
        }

        // Try systemd first
        if (await _systemdStarter.TryStartAsync(service, cancellationToken))
        {
            await Task.Delay(SERVICE_STARTUP_DELAY_MS, cancellationToken);

            if (await _healthMonitor.CheckHealthAsync(service, cancellationToken))
            {
                service.IsRunning = true;
                return;
            }
        }

        // Fall back to dotnet run
        _logger.LogInformation("Systemd start failed or unavailable, trying dotnet run for {ServiceName}", service.Name);

        await _processStarter.StartAsync(service, cancellationToken);
        await Task.Delay(SERVICE_STARTUP_DELAY_MS, cancellationToken);

        if (await _healthMonitor.CheckHealthAsync(service, cancellationToken))
        {
            service.IsRunning = true;
            _logger.LogInformation("{ServiceName} started successfully", service.Name);
        }
        else
        {
            _logger.LogError("{ServiceName} failed to start (health check failed)", service.Name);
        }
    }

    /// <inheritdoc/>
    public async Task StopServiceAsync(DependentServiceInfo service, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping {ServiceName}...", service.Name);

        // If started via dotnet run (has process handle), kill the process
        if (service.Process != null && !service.Process.HasExited)
        {
            try
            {
                _processManager.Kill(service.Process);
                _logger.LogInformation("Killed {ServiceName} process (PID: {ProcessId})",
                    service.Name, service.Process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill {ServiceName} process", service.Name);
            }
        }

        // Try systemd stop
        await _systemdStarter.StopAsync(service, cancellationToken);

        service.IsRunning = false;
        _logger.LogInformation("{ServiceName} stopped", service.Name);
    }
}
