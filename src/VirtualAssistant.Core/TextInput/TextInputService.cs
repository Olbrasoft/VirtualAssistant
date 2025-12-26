using Microsoft.Extensions.Logging;
using OpenCode.DotnetClient;

namespace Olbrasoft.VirtualAssistant.Core.TextInput;

/// <summary>
/// Service for sending text to OpenCode via HTTP API.
/// Uses OpenCode.DotnetClient for TUI API communication.
/// </summary>
public class TextInputService : ITextInputService
{
    private readonly ILogger<TextInputService> _logger;
    private readonly OpenCodeClient _openCodeClient;

    public TextInputService(ILogger<TextInputService> logger, OpenCodeClient openCodeClient)
    {
        _logger = logger;
        _openCodeClient = openCodeClient;
    }

    /// <summary>
    /// Sends a message to OpenCode via TUI API (asynchronous, fire-and-forget).
    /// Uses AppendPrompt to set the text, then SubmitPrompt to send it.
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

        // Prepend mode prefix so OpenCode knows which agent to use
        var modePrefix = agent.Equals("plan", StringComparison.OrdinalIgnoreCase) 
            ? "[PLAN MODE - READ ONLY] " 
            : "[BUILD MODE] ";
        var fullText = modePrefix + text;

        try
        {
            _logger.LogDebug("Sending message with agent '{Agent}' via TUI API", agent);

            await _openCodeClient.SendToTuiAsync(fullText, cancellationToken);

            _logger.LogInformation("📤 Sent message via TUI API with agent '{Agent}'", agent);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "❌ OpenCode TUI API not reachable");
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

        try
        {
            await _openCodeClient.AppendPromptAsync(text, cancellationToken);

            if (submitPrompt)
            {
                await Task.Delay(100, cancellationToken);
                await _openCodeClient.SubmitPromptAsync(cancellationToken);
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "❌ OpenCode API not reachable");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send text to OpenCode: {Message}", ex.Message);
            return false;
        }
    }
}
