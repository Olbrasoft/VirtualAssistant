namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Result of location check.
/// </summary>
public sealed record LocationInfo(
    string CountryCode,
    bool IsVpnDetected,
    string? City = null,
    string? Isp = null);

/// <summary>
/// Interface for detecting user's location and VPN status.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Gets the current location information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Location information</returns>
    Task<LocationInfo> GetLocationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if VPN is currently active (location is not in Czech Republic).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if VPN is detected</returns>
    Task<bool> IsVpnActiveAsync(CancellationToken cancellationToken = default);
}
