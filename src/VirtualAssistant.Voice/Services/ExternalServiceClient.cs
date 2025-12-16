using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Dtos;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Client for calling external services (PTT, task dispatch).
/// </summary>
public class ExternalServiceClient : IExternalServiceClient
{
    private readonly ILogger<ExternalServiceClient> _logger;
    private readonly HttpClient _httpClient;

    // Service endpoints
    private const string PttRepeatEndpoint = "http://localhost:5050/api/ptt/repeat";
    private const string TaskDispatchEndpoint = "http://localhost:5055/api/hub/dispatch-task";

    public ExternalServiceClient(
        ILogger<ExternalServiceClient> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<(bool Success, PttRepeatResponse? Response, string? Error)> CallPttRepeatAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Calling PTT repeat endpoint");

            var response = await _httpClient.PostAsync(PttRepeatEndpoint, null, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PttRepeatResponse>(cancellationToken: ct);
                return (true, result, null);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, null, "No text in history");
            }

            return (false, null, $"HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call PTT repeat endpoint");
            return (false, null, $"Service unavailable: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling PTT repeat endpoint");
            return (false, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, VoiceDispatchTaskResponse? Response, string? Error)> DispatchTaskAsync(string targetAgent, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Dispatching task to {Agent}", targetAgent);

            var requestBody = new { agent = targetAgent };
            var response = await _httpClient.PostAsJsonAsync(TaskDispatchEndpoint, requestBody, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<VoiceDispatchTaskResponse>(cancellationToken: ct);
                return (true, result, null);
            }

            return (false, null, $"HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call dispatch task endpoint");
            return (false, null, $"Service unavailable: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching task");
            return (false, null, ex.Message);
        }
    }
}
