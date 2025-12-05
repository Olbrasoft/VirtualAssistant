namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

/// <summary>
/// Single-level history for storing the last transcribed text.
/// Allows re-pasting text if it was accidentally pasted to the wrong window.
/// </summary>
public interface ITranscriptionHistory
{
    /// <summary>
    /// Gets the last transcribed text, or null if no transcription has been made.
    /// </summary>
    string? LastText { get; }
    
    /// <summary>
    /// Saves the transcribed text to history. Overwrites any previous text.
    /// </summary>
    /// <param name="text">The transcribed text to save.</param>
    void SaveText(string text);
    
    /// <summary>
    /// Clears the transcription history.
    /// </summary>
    void Clear();
}
