namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Detects special commands in transcribed text.
/// </summary>
public interface ICommandDetectionService
{
    /// <summary>
    /// Checks if the text contains a stop command.
    /// </summary>
    bool IsStopCommand(string text);

    /// <summary>
    /// Checks if the text contains a cancel command.
    /// </summary>
    bool IsCancelCommand(string text);

    /// <summary>
    /// Checks if the text should be skipped locally (noise phrases).
    /// </summary>
    bool ShouldSkipLocally(string text);
}
