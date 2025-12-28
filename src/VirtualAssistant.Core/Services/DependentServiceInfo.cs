using System.Diagnostics;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Configuration and runtime information for a dependent service.
/// </summary>
public class DependentServiceInfo
{
    /// <summary>
    /// Gets the name of the service.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the health check endpoint URL for the service.
    /// </summary>
    public string HealthCheckUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the systemd service name (used for starting/stopping via systemctl).
    /// </summary>
    public string SystemdServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the path to the .csproj file (used for starting via dotnet run).
    /// </summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the service is currently running.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Gets or sets the process handle if the service was started via dotnet run.
    /// </summary>
    public Process? Process { get; set; }
}
