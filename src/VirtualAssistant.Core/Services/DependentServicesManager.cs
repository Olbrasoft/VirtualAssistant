using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Coordinates lifecycle of dependent services (e.g., TextToSpeech.Service).
/// Delegates to specialized services for health monitoring, systemd control, process management, and port-based killing.
/// </summary>
public class DependentServicesManager : IDependentServiceManager
{
    private readonly ILogger<DependentServicesManager> _logger;
    private readonly IServiceHealthMonitor _healthMonitor;
    private readonly ISystemdServiceController _systemdController;
    private readonly IProcessManager _processManager;
    private readonly IPortBasedProcessKiller _portKiller;
    private readonly Dictionary<string, DependentServiceInfo> _services = new();
    private readonly CancellationTokenSource _monitoringCts = new();
    private Task? _monitoringTask;
    private bool _disposed;

    public event EventHandler<ServiceStatusChangedEventArgs>? ServiceStatusChanged;

    public DependentServicesManager(
        ILogger<DependentServicesManager> logger,
        IServiceHealthMonitor healthMonitor,
        ISystemdServiceController systemdController,
        IProcessManager processManager,
        IPortBasedProcessKiller portKiller)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _systemdController = systemdController ?? throw new ArgumentNullException(nameof(systemdController));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _portKiller = portKiller ?? throw new ArgumentNullException(nameof(portKiller));

        // Register dependent services
        _services.Add("TextToSpeech.Service", new DependentServiceInfo
        {
            Name = "TextToSpeech.Service",
            HealthCheckUrl = "http://localhost:5060/health",
            SystemdServiceName = "text-to-speech.service",
            ProjectPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Olbrasoft/TextToSpeech/src/TextToSpeech.Service/TextToSpeech.Service.csproj"
            )
        });
    }

    public async Task StartServicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting dependent services...");

        foreach (var service in _services.Values)
        {
            try
            {
                var isHealthy = await _healthMonitor.CheckHealthAsync(service.HealthCheckUrl, cancellationToken);

                if (isHealthy)
                {
                    _logger.LogInformation("{ServiceName} is already running", service.Name);
                    service.IsRunning = true;
                    OnServiceStatusChanged(service.Name, true);
                    continue;
                }

                // Try to start via systemd first
                var serviceExists = await _systemdController.ServiceExistsAsync(service.SystemdServiceName, cancellationToken);
                var started = false;

                if (serviceExists)
                {
                    started = await _systemdController.StartServiceAsync(service.SystemdServiceName, cancellationToken);
                }

                if (!started)
                {
                    // Fallback to dotnet run
                    _logger.LogWarning("Systemd service {SystemdServiceName} not found or failed to start, falling back to dotnet run",
                        service.SystemdServiceName);
                    service.Process = await _processManager.StartDotnetRunAsync(service.ProjectPath, service.Name, cancellationToken);
                }

                // Wait a bit for service to start
                await Task.Delay(2000, cancellationToken);

                // Verify service is healthy
                isHealthy = await _healthMonitor.CheckHealthAsync(service.HealthCheckUrl, cancellationToken);
                service.IsRunning = isHealthy;

                if (isHealthy)
                {
                    _logger.LogInformation("{ServiceName} started successfully", service.Name);
                }
                else
                {
                    _logger.LogError("{ServiceName} failed to start or become healthy", service.Name);
                }

                OnServiceStatusChanged(service.Name, isHealthy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start {ServiceName}", service.Name);
                service.IsRunning = false;
                OnServiceStatusChanged(service.Name, false);
            }
        }

        // Start monitoring
        var serviceHealthCheckUrls = _services.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.HealthCheckUrl);
        _monitoringTask = _healthMonitor.MonitorServicesAsync(
            serviceHealthCheckUrls,
            (serviceName, isRunning) =>
            {
                if (_services.TryGetValue(serviceName, out var service))
                {
                    service.IsRunning = isRunning;
                }
                OnServiceStatusChanged(serviceName, isRunning);
            },
            TimeSpan.FromSeconds(30),
            _monitoringCts.Token);
    }

    public async Task StopServicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping dependent services...");

        // Stop monitoring first
        _monitoringCts.Cancel();
        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        foreach (var service in _services.Values)
        {
            try
            {
                if (service.Process != null && !service.Process.HasExited)
                {
                    _logger.LogInformation("Stopping {ServiceName} (dotnet run process)", service.Name);
                    await _processManager.KillProcessAsync(service.Process, service.Name, cancellationToken);
                    service.Process = null;
                }
                else if (!string.IsNullOrEmpty(service.SystemdServiceName))
                {
                    // If started via systemd, we don't stop it (user may want to keep it running)
                    _logger.LogInformation("Skipping shutdown of systemd service {SystemdServiceName}",
                        service.SystemdServiceName);
                }

                service.IsRunning = false;
                OnServiceStatusChanged(service.Name, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop {ServiceName}", service.Name);
            }
        }
    }

    public IDictionary<string, bool> GetServicesStatus()
    {
        return _services.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.IsRunning);
    }

    public async Task RefreshServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        _logger.LogInformation("Refreshing status for {ServiceName}", serviceName);

        var wasRunning = service.IsRunning;
        var isHealthy = await _healthMonitor.CheckHealthAsync(service.HealthCheckUrl, cancellationToken);
        service.IsRunning = isHealthy;

        if (wasRunning != isHealthy)
        {
            _logger.LogInformation("{ServiceName} status changed: {Status}",
                serviceName, isHealthy ? "Running" : "Stopped");
            OnServiceStatusChanged(serviceName, isHealthy);
        }
        else
        {
            _logger.LogInformation("{ServiceName} status confirmed: {Status}",
                serviceName, isHealthy ? "Running" : "Stopped");
            // Fire event even if status unchanged to update UI
            OnServiceStatusChanged(serviceName, isHealthy);
        }
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        _logger.LogInformation("Starting {ServiceName}", serviceName);

        // Check if already running
        var isHealthy = await _healthMonitor.CheckHealthAsync(service.HealthCheckUrl, cancellationToken);
        if (isHealthy)
        {
            _logger.LogInformation("{ServiceName} is already running", serviceName);
            service.IsRunning = true;
            OnServiceStatusChanged(serviceName, true);
            return;
        }

        // Try to start via systemd first
        var serviceExists = await _systemdController.ServiceExistsAsync(service.SystemdServiceName, cancellationToken);
        var started = false;

        if (serviceExists)
        {
            started = await _systemdController.StartServiceAsync(service.SystemdServiceName, cancellationToken);
        }

        if (!started)
        {
            // Fallback to dotnet run
            _logger.LogWarning("Systemd service {SystemdServiceName} not found or failed to start, falling back to dotnet run",
                service.SystemdServiceName);
            service.Process = await _processManager.StartDotnetRunAsync(service.ProjectPath, service.Name, cancellationToken);
        }

        // Wait a bit for service to start
        await Task.Delay(2000, cancellationToken);

        // Verify service is healthy
        isHealthy = await _healthMonitor.CheckHealthAsync(service.HealthCheckUrl, cancellationToken);
        service.IsRunning = isHealthy;

        if (isHealthy)
        {
            _logger.LogInformation("{ServiceName} started successfully", serviceName);
        }
        else
        {
            _logger.LogError("{ServiceName} failed to start or become healthy", serviceName);
        }

        OnServiceStatusChanged(serviceName, isHealthy);
    }

    public async Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        _logger.LogInformation("Stopping {ServiceName}", serviceName);

        bool stopped = false;

        // Try 1: Kill tracked process
        if (service.Process != null && !service.Process.HasExited)
        {
            _logger.LogInformation("Stopping {ServiceName} (dotnet run process)", serviceName);
            await _processManager.KillProcessAsync(service.Process, serviceName, cancellationToken);
            service.Process = null;
            stopped = true;
        }
        // Try 2: Stop via systemd
        else if (!string.IsNullOrEmpty(service.SystemdServiceName))
        {
            stopped = await _systemdController.StopServiceAsync(service.SystemdServiceName, cancellationToken);
        }

        // Try 3: Find and kill process by port from HealthCheckUrl
        if (!stopped)
        {
            try
            {
                // Extract port from health check URL (e.g., "http://localhost:5060/health" -> 5060)
                var uri = new Uri(service.HealthCheckUrl);
                var port = uri.Port;

                _logger.LogInformation("Attempting to stop {ServiceName} by finding process on port {Port}", serviceName, port);

                stopped = await _portKiller.KillProcessByPortAsync(port, cancellationToken);

                if (!stopped)
                {
                    _logger.LogWarning("No process found listening on port {Port} for {ServiceName}", port, serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop {ServiceName} by port", serviceName);
            }
        }

        service.IsRunning = false;
        OnServiceStatusChanged(serviceName, false);
    }

    private void OnServiceStatusChanged(string serviceName, bool isRunning)
    {
        ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs
        {
            ServiceName = serviceName,
            IsRunning = isRunning
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _monitoringCts.Cancel();
        _monitoringCts.Dispose();

        foreach (var service in _services.Values)
        {
            service.Process?.Dispose();
        }
    }

    private class DependentServiceInfo
    {
        public string Name { get; init; } = string.Empty;
        public string HealthCheckUrl { get; init; } = string.Empty;
        public string SystemdServiceName { get; init; } = string.Empty;
        public string ProjectPath { get; init; } = string.Empty;
        public bool IsRunning { get; set; }
        public Process? Process { get; set; }
    }
}
