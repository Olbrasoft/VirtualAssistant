using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Coordinates lifecycle management of dependent services (e.g., TextToSpeech.Service).
/// Simplified coordinator pattern - delegates to specialized services.
/// </summary>
public class DependentServicesManager : IDependentServiceManager
{
    private readonly ILogger<DependentServicesManager> _logger;
    private readonly IServiceLifecycleManager _lifecycle;
    private readonly IServiceHealthMonitor _healthMonitor;
    private readonly Dictionary<string, DependentServiceInfo> _services = new();
    private readonly CancellationTokenSource _monitoringCts = new();
    private Task? _monitoringTask;
    private bool _disposed;

    public event EventHandler<ServiceStatusChangedEventArgs>? ServiceStatusChanged;

    public DependentServicesManager(
        ILogger<DependentServicesManager> logger,
        IServiceLifecycleManager lifecycle,
        IServiceHealthMonitor healthMonitor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));

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

    /// <summary>
    /// Starts all configured dependent services.
    /// </summary>
    public async Task StartServicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting dependent services...");

        foreach (var service in _services.Values)
        {
            try
            {
                await _lifecycle.StartServiceAsync(service, cancellationToken);
                OnServiceStatusChanged(service.Name, service.IsRunning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start {ServiceName}", service.Name);
            }
        }

        // Start background monitoring
        _monitoringTask = _healthMonitor.MonitorAsync(
            _services.Values,
            OnServiceStatusChanged,
            _monitoringCts.Token);

        _logger.LogInformation("All dependent services processed");
    }

    /// <summary>
    /// Stops all running dependent services.
    /// </summary>
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

        // Stop all services
        foreach (var service in _services.Values.Where(s => s.IsRunning))
        {
            try
            {
                await _lifecycle.StopServiceAsync(service, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop {ServiceName}", service.Name);
            }
        }

        _logger.LogInformation("All dependent services stopped");
    }

    /// <summary>
    /// Gets the current running status of all services.
    /// </summary>
    public IDictionary<string, bool> GetServicesStatus()
    {
        return _services.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.IsRunning);
    }

    /// <summary>
    /// Refreshes the health status of a specific service.
    /// </summary>
    public async Task RefreshServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        var wasRunning = service.IsRunning;
        var isHealthy = await _healthMonitor.CheckHealthAsync(service, cancellationToken);
        service.IsRunning = isHealthy;

        if (wasRunning != isHealthy)
        {
            _logger.LogInformation("{ServiceName} status changed to {Status}",
                serviceName, isHealthy ? "Running" : "Stopped");
            OnServiceStatusChanged(serviceName, isHealthy);
        }
    }

    /// <summary>
    /// Starts a specific service by name.
    /// </summary>
    public async Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        await _lifecycle.StartServiceAsync(service, cancellationToken);
        OnServiceStatusChanged(service.Name, service.IsRunning);
    }

    /// <summary>
    /// Stops a specific service by name.
    /// </summary>
    public async Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        await _lifecycle.StopServiceAsync(service, cancellationToken);
        OnServiceStatusChanged(service.Name, service.IsRunning);
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

        // Dispose processes
        foreach (var service in _services.Values)
        {
            service.Process?.Dispose();
        }
    }
}
