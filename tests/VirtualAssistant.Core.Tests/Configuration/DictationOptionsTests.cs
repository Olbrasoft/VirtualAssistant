using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Configuration;

/// <summary>
/// Mirrors the ContinuousListenerOptions tests for the dictation-specific
/// path-resolution branches. The filename-only branch delegates to
/// WhisperModelLocator which walks real XDG directories; that path needs
/// a filesystem fixture and is covered separately.
/// </summary>
public class DictationOptionsTests
{
    [Fact]
    public void GetFullWhisperModelPath_AbsolutePath_IsReturnedVerbatim()
    {
        var abs = Path.Combine(Path.GetTempPath(), "ggml-large-v3-turbo.bin");
        var options = new DictationOptions { WhisperModelPath = abs };

        Assert.Equal(abs, options.GetFullWhisperModelPath());
    }

    [Fact]
    public void GetFullWhisperModelPath_RelativeWithSeparator_IsJoinedToBaseDirectory()
    {
        var options = new DictationOptions { WhisperModelPath = "models/custom.bin" };

        var expected = Path.Combine(AppContext.BaseDirectory, "models/custom.bin");
        Assert.Equal(expected, options.GetFullWhisperModelPath());
    }

    [Fact]
    public void Defaults_MatchDictationTunedWhisperTurbo()
    {
        // Dictation picks the larger "-turbo" variant (more accurate than the
        // "medium" the continuous listener runs) because the user waits for it
        // synchronously. Pin the defaults so a silent config drift doesn't
        // downgrade dictation quality.
        var options = new DictationOptions();

        Assert.Equal("ggml-large-v3-turbo.bin", options.WhisperModelPath);
        Assert.Equal("cs", options.WhisperLanguage);
        Assert.Equal(16000, options.SampleRate);
        Assert.Equal(50, options.KeyboardLedSettleTimeMs);
        Assert.Equal(300, options.MaxDurationSeconds);
    }
}
