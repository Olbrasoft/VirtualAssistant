using System.ComponentModel.DataAnnotations;

namespace Olbrasoft.VirtualAssistant.Service.Configuration;

/// <summary>
/// Configuration options for service lifecycle monitoring.
/// </summary>
public class ServiceMonitoringOptions
{
    public const string SectionName = "ServiceMonitoring";

    /// <summary>
    /// Timeout for status polling in milliseconds.
    /// Default: 2000 ms (2 seconds).
    /// </summary>
    [Range(100, 30000, ErrorMessage = "StatusPollTimeoutMs must be between 100 and 30000 ms")]
    public int StatusPollTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Interval between status polls in milliseconds.
    /// Default: 100 ms.
    /// </summary>
    [Range(10, 5000, ErrorMessage = "StatusPollIntervalMs must be between 10 and 5000 ms")]
    public int StatusPollIntervalMs { get; set; } = 100;
}
