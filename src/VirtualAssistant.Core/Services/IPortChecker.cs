namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides functionality to check port availability.
/// </summary>
public interface IPortChecker
{
    /// <summary>
    /// Checks if a port is available (not in use).
    /// </summary>
    /// <param name="url">URL containing the port to check (e.g., "http://localhost:5060/health").</param>
    /// <returns>True if port is available, false if in use.</returns>
    bool IsPortAvailable(string url);
}
