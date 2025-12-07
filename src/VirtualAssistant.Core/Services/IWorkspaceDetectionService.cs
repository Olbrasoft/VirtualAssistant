namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for detecting GNOME workspaces to optimize TTS notifications.
/// Allows skipping notifications when user is on the same workspace as the agent.
/// </summary>
public interface IWorkspaceDetectionService
{
    /// <summary>
    /// Gets the current active workspace number.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Workspace number (0-based), or -1 if detection fails</returns>
    Task<int> GetCurrentWorkspaceAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the workspace number where the agent's terminal window is located.
    /// </summary>
    /// <param name="agentName">Agent name (e.g., "opencode", "claude")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Workspace number (0-based), or -1 if window not found</returns>
    Task<int> GetAgentWorkspaceAsync(string agentName, CancellationToken ct = default);

    /// <summary>
    /// Checks if user is on the same workspace as the agent.
    /// </summary>
    /// <param name="agentName">Agent name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if same workspace (skip notification), false otherwise</returns>
    Task<bool> IsUserOnAgentWorkspaceAsync(string agentName, CancellationToken ct = default);
}
