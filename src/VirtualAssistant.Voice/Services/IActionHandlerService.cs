using Olbrasoft.VirtualAssistant.Core.Enums;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for handling LLM router actions.
/// Provides shared action handling logic used by multiple workers.
/// </summary>
public interface IActionHandlerService
{
    /// <summary>
    /// Handles OpenCode action by sending command to appropriate agent based on prompt type.
    /// </summary>
    /// <param name="command">The command text to send.</param>
    /// <param name="promptType">The type of prompt (determines agent selection).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleOpenCodeActionAsync(string command, PromptType? promptType, CancellationToken cancellationToken);

    /// <summary>
    /// Handles respond action by logging the response.
    /// </summary>
    /// <param name="response">The response text from LLM.</param>
    void HandleRespondAction(string? response);

    /// <summary>
    /// Handles repeat text action by copying last text to clipboard and announcing via TTS.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleRepeatTextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Handles dispatch task action by sending task to target agent.
    /// </summary>
    /// <param name="targetAgent">The target agent name (e.g., "claude", "build").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleDispatchTaskActionAsync(string targetAgent, CancellationToken cancellationToken);
}
