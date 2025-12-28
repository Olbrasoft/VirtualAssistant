using Olbrasoft.VirtualAssistant.Core.Enums;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Handles common action processing logic shared between workers.
/// </summary>
public interface IActionHandlerService
{
    /// <summary>
    /// Handles OpenCode action by sending command to OpenCode with appropriate agent.
    /// </summary>
    /// <param name="command">The command text to send.</param>
    /// <param name="promptType">Type of prompt to determine which agent to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleOpenCodeAsync(string command, PromptType? promptType, CancellationToken cancellationToken);

    /// <summary>
    /// Handles repeat text action by fetching last transcribed text from PTT history.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleRepeatTextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Handles dispatch task action by sending task to target agent.
    /// </summary>
    /// <param name="targetAgent">The target agent name (e.g., "claude").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleDispatchTaskAsync(string targetAgent, CancellationToken cancellationToken);
}
