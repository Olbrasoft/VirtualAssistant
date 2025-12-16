namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Detects special commands in transcribed text.
/// </summary>
public class CommandDetectionService : ICommandDetectionService
{
    private static readonly HashSet<string> StopCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "stop"
    };

    private static readonly HashSet<string> CancelCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cancel",
        "kencel"  // Possible Whisper transcription variant
    };

    private static readonly HashSet<string> NoisePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ano", "ne", "jo", "no", "tak", "hmm", "hm", "aha", "ok", "okay",
        "dobre", "jasne", "fajn", "super", "diky", "dekuji", "prosim",
        "moment", "pockej", "ehm", "ehm ehm", "no tak", "tak jo",
        "to je", "to bylo", "a tak", "no jo", "no ne", "tak tak",
        "jasne jasne", "jo jo", "ne ne", "aha aha", "mm", "mhm",
        "no nic", "nic", "nevim", "uvidime", "mozna", "asi",
        "co to", "co je", "hele", "hele hele", "vis co", "ze jo",
        "no jasne", "no dobre", "no fajn", "to jo", "to ne",
        "tak nejak", "nejak", "proste", "vlastne", "takze",
        "...", ".", ",", "!", "?"
    };

    /// <inheritdoc />
    public bool IsStopCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim().ToLowerInvariant();
        var words = normalized.Split([' ', ',', '.', '!', '?', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (StopCommands.Contains(word))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsCancelCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim().ToLowerInvariant();
        var words = normalized.Split([' ', ',', '.', '!', '?', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (CancelCommands.Contains(word))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool ShouldSkipLocally(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return NoisePatterns.Contains(normalized.TrimEnd('.', ',', '!', '?', ' '));
    }
}
