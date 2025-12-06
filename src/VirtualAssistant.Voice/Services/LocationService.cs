using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for detecting user's location using ip-api.com.
/// Used to determine if VPN is active (non-CZ location).
/// </summary>
public sealed class LocationService : ILocationService
{
    private const string IP_API_URL = "http://ip-api.com/json/?fields=status,countryCode,city,isp,proxy,hosting";
    private const string HOME_COUNTRY = "CZ";
    private const int CACHE_DURATION_SECONDS = 300; // 5 minutes

    private readonly HttpClient _httpClient;
    private readonly ILogger<LocationService> _logger;

    private LocationInfo? _cachedLocation;
    private DateTime _cacheExpiry = DateTime.MinValue;

    public LocationService(HttpClient httpClient, ILogger<LocationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LocationInfo> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        // Return cached result if still valid
        if (_cachedLocation != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedLocation;
        }

        try
        {
            var response = await _httpClient.GetFromJsonAsync<IpApiResponse>(IP_API_URL, cancellationToken);

            if (response == null || response.Status != "success")
            {
                _logger.LogWarning("ip-api.com returned invalid response");
                return new LocationInfo(HOME_COUNTRY, false);
            }

            var isVpn = response.CountryCode != HOME_COUNTRY || response.Proxy || response.Hosting;

            _cachedLocation = new LocationInfo(
                response.CountryCode ?? HOME_COUNTRY,
                isVpn,
                response.City,
                response.Isp);

            _cacheExpiry = DateTime.UtcNow.AddSeconds(CACHE_DURATION_SECONDS);

            _logger.LogInformation("Location detected: {Country} (VPN: {IsVpn})",
                _cachedLocation.CountryCode, isVpn);

            return _cachedLocation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get location from ip-api.com");
            // On error, assume no VPN (fail-open)
            return new LocationInfo(HOME_COUNTRY, false);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsVpnActiveAsync(CancellationToken cancellationToken = default)
    {
        var location = await GetLocationAsync(cancellationToken);
        return location.IsVpnDetected;
    }

    private sealed class IpApiResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("isp")]
        public string? Isp { get; set; }

        [JsonPropertyName("proxy")]
        public bool Proxy { get; set; }

        [JsonPropertyName("hosting")]
        public bool Hosting { get; set; }
    }
}
