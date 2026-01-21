namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Detects CLI applications running inside terminal emulators.
/// Used to identify apps like Claude Code or OpenCode when they change the terminal title.
/// </summary>
public interface ICliAppDetector
{
    /// <summary>
    /// Detects if a known CLI application is running in any terminal.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The name of the detected CLI app (e.g., "Claude Code", "OpenCode") or null if none detected.
    /// </returns>
    Task<CliAppDetectionResult?> DetectCliAppAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of CLI app detection.
/// </summary>
/// <param name="AppName">Display name of the detected app (e.g., "Claude Code").</param>
/// <param name="PromptFileName">The prompt file to use for this app (e.g., "ClaudeCodeCorrection").</param>
public record CliAppDetectionResult(string AppName, string PromptFileName);
