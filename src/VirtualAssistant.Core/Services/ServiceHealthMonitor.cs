using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Monitors service health via HTTP health check endpoints.
/// </summary>
public class ServiceHealthMonitor : IServiceHealthMonitor
{
    /// <summary>
    /// Interval between health checks in seconds.
    /// </summary>
    private const int HEALTH_CHECK_INTERVAL_SECONDS = 30;

    private readonly ILogger<ServiceHealthMonitor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ServiceHealthMonitor(
        ILogger<ServiceHealthMonitor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc/>
    public async Task<bool> CheckHealthAsync(DependentServiceInfo service, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);

            using var response = await httpClient.GetAsync(service.HealthCheckUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task MonitorAsync(
        IEnumerable<DependentServiceInfo> services,
        Action<string, bool> onStatusChanged,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting service health monitoring");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var service in services)
                {
                    var wasRunning = service.IsRunning;
                    var isHealthy = await CheckHealthAsync(service, cancellationToken);
                    service.IsRunning = isHealthy;

                    if (wasRunning != isHealthy)
                    {
                        _logger.LogWarning("{ServiceName} status changed: {Status}",
                            service.Name, isHealthy ? "Running" : "Stopped");
                        onStatusChanged(service.Name, isHealthy);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(HEALTH_CHECK_INTERVAL_SECONDS), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during service health monitoring");
            }
        }

        _logger.LogInformation("Service health monitoring stopped");
    }
}
