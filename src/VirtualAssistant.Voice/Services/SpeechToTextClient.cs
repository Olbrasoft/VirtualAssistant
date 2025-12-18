using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// HTTP client for querying SpeechToText application status.
/// </summary>
public class SpeechToTextClient : ISpeechToTextClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpeechToTextClient> _logger;
    private readonly string _statusUrl;

    public SpeechToTextClient(
        HttpClient httpClient,
        ILogger<SpeechToTextClient> logger,
        IOptions<SpeechToTextSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _statusUrl = $"{settings.Value.BaseUrl.TrimEnd('/')}/api/status";

        _logger.LogDebug("SpeechToTextClient initialized with status URL: {Url}", _statusUrl);
    }

    /// <inheritdoc />
    public async Task<SpeechToTextStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(_statusUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SpeechToText status request failed with status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var status = await response.Content.ReadFromJsonAsync<SpeechToTextStatus>(cancellationToken);
            return status;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "SpeechToText service unavailable at {Url}", _statusUrl);
            return null;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogDebug(ex, "SpeechToText status request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error querying SpeechToText status");
            return null;
        }
    }
}
