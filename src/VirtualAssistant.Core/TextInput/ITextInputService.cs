namespace Olbrasoft.VirtualAssistant.Core.TextInput;

/// <summary>
/// Service for sending text to OpenCode via HTTP API.
/// </summary>
public interface ITextInputService
{
    /// <summary>
    /// Sends a message to OpenCode via TUI API (asynchronous, fire-and-forget).
    /// Uses AppendPrompt to set the text, then SubmitPrompt to send it.
    /// </summary>
    /// <param name="text">Message text to send.</param>
    /// <param name="agent">Agent to use: "plan" for questions/discussions, "build" for commands.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if message was sent successfully, false otherwise.</returns>
    Task<bool> SendMessageToSessionAsync(string text, string agent = "build", CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends text to OpenCode via HTTP POST to /tui/append-prompt endpoint.
    /// Optionally submits the prompt with /tui/submit-prompt.
    /// </summary>
    Task<bool> TypeTextAsync(string text, bool submitPrompt = false, CancellationToken cancellationToken = default);
}
