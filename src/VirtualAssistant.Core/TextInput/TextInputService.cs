using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.TextInput;

/// <summary>
/// Service for sending text to OpenCode via HTTP API.
/// Supports TUI API for prompt submission.
/// </summary>
public class TextInputService
{
    private readonly ILogger<TextInputService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public TextInputService(ILogger<TextInputService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Sends a message to OpenCode via TUI API (asynchronous, fire-and-forget).
    /// Uses /tui/append-prompt to set the text, then /tui/submit-prompt to send it.
    /// </summary>
    /// <param name="text">Message text to send.</param>
    /// <param name="agent">Agent to use: "plan" for questions/discussions, "build" for commands.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if message was sent successfully, false otherwise.</returns>
    public async Task<bool> SendMessageToSessionAsync(string text, string agent = "build", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Cannot send empty message");
            return false;
        }

        var openCodeUrl = _configuration["OpenCodeUrl"] ?? "http://localhost:4096";
        var baseUrl = openCodeUrl.TrimEnd('/');

        // Prepend mode prefix so OpenCode knows which agent to use
        var modePrefix = agent.Equals("plan", StringComparison.OrdinalIgnoreCase) 
            ? "[PLAN MODE - READ ONLY] " 
            : "[BUILD MODE] ";
        var fullText = modePrefix + text;

        try
        {
            // Step 1: Append text to prompt (includes mode prefix)
            var appendEndpoint = $"{baseUrl}/tui/append-prompt";
            var appendPayload = new { text = fullText };
            var appendJson = JsonSerializer.Serialize(appendPayload);
            var appendContent = new StringContent(appendJson, Encoding.UTF8, "application/json");

            _logger.LogDebug("Appending message with agent '{Agent}' via TUI API", agent);

            var appendResponse = await _httpClient.PostAsync(appendEndpoint, appendContent, cancellationToken);
            if (!appendResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to append prompt: {StatusCode}", appendResponse.StatusCode);
                return false;
            }

            // Small delay to ensure text is appended before submitting
            await Task.Delay(50, cancellationToken);

            // Step 2: Submit the prompt (fire-and-forget, doesn't wait for AI response)
            var submitEndpoint = $"{baseUrl}/tui/submit-prompt";
            var submitResponse = await _httpClient.PostAsync(submitEndpoint, null, cancellationToken);

            if (!submitResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to submit prompt: {StatusCode}", submitResponse.StatusCode);
                return false;
            }

            _logger.LogInformation("📤 Sent message via TUI API with agent '{Agent}'", agent);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "❌ OpenCode TUI API not reachable at {Url}", baseUrl);
            return false;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            _logger.LogWarning(ex, "❌ OpenCode TUI API timeout");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send message via TUI API: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Sends text to OpenCode via HTTP POST to /tui/append-prompt endpoint.
    /// Optionally submits the prompt with /tui/submit-prompt.
    /// </summary>
    public async Task<bool> TypeTextAsync(string text, bool submitPrompt = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Cannot send empty text");
            return false;
        }

        var openCodeUrl = _configuration["OpenCodeUrl"] ?? "http://localhost:4096";
        var baseUrl = openCodeUrl.TrimEnd('/');

        try
        {
            // Step 1: Append text to prompt
            var appendEndpoint = $"{baseUrl}/tui/append-prompt";
            
            var payload = new { text };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(appendEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenCode API returned {StatusCode}", response.StatusCode);
                return false;
            }

            // Step 2: Submit prompt if requested
            if (submitPrompt)
            {
                await Task.Delay(100, cancellationToken);
                
                var submitEndpoint = $"{baseUrl}/tui/submit-prompt";
                var submitResponse = await _httpClient.PostAsync(submitEndpoint, null, cancellationToken);

                if (!submitResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to submit prompt: {StatusCode}", submitResponse.StatusCode);
                }
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "❌ OpenCode API not reachable at {Url}", baseUrl);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send text to OpenCode: {Message}", ex.Message);
            return false;
        }
    }
}
