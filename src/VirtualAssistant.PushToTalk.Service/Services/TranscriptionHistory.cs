namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

/// <summary>
/// Thread-safe singleton implementation of single-level transcription history.
/// Stores only the last transcribed text (new text overwrites old).
/// </summary>
public class TranscriptionHistory : ITranscriptionHistory
{
    private readonly object _lock = new();
    private string? _lastText;

    /// <inheritdoc />
    public string? LastText
    {
        get
        {
            lock (_lock)
            {
                return _lastText;
            }
        }
    }

    /// <inheritdoc />
    public void SaveText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
            
        lock (_lock)
        {
            _lastText = text;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _lastText = null;
        }
    }
}
