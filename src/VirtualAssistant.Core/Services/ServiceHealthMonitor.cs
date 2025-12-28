using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Monitors service health via HTTP health check endpoints.
/// Responsible for checking if services are healthy and monitoring them periodically.
/// </summary>
public class ServiceHealthMonitor : IServiceHealthMonitor
{
    private readonly ILogger<ServiceHealthMonitor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ServiceHealthMonitor(
        ILogger<ServiceHealthMonitor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// Checks if a service is healthy by calling its health check endpoint.
    /// </summary>
    /// <param name="healthCheckUrl">The health check endpoint URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is healthy, false otherwise.</returns>
    public async Task<bool> CheckHealthAsync(string healthCheckUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);

            var response = await httpClient.GetAsync(healthCheckUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health check failed for {HealthCheckUrl}", healthCheckUrl);
            return false;
        }
    }

    /// <summary>
    /// Monitors services periodically and invokes callback when status changes.
    /// </summary>
    /// <param name="services">Dictionary of service names to health check URLs.</param>
    /// <param name="statusChangedCallback">Callback invoked when service status changes.</param>
    /// <param name="checkInterval">How often to check service health.</param>
    /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
    public async Task MonitorServicesAsync(
        IDictionary<string, string> services,
        Action<string, bool> statusChangedCallback,
        TimeSpan checkInterval,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting service health monitoring (interval: {Interval})", checkInterval);

        var serviceStatuses = new Dictionary<string, bool>();
        foreach (var serviceName in services.Keys)
        {
            serviceStatuses[serviceName] = false;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(checkInterval, cancellationToken);

                foreach (var (serviceName, healthCheckUrl) in services)
                {
                    var wasRunning = serviceStatuses[serviceName];
                    var isHealthy = await CheckHealthAsync(healthCheckUrl, cancellationToken);
                    serviceStatuses[serviceName] = isHealthy;

                    if (wasRunning != isHealthy)
                    {
                        _logger.LogWarning("{ServiceName} status changed: {Status}",
                            serviceName, isHealthy ? "Running" : "Stopped");
                        statusChangedCallback(serviceName, isHealthy);
                    }
                }
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
