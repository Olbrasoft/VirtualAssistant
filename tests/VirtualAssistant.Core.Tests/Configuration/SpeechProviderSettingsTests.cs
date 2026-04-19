using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Configuration;

/// <summary>
/// Pins the defaults on SpeechProviderSettings: the whole fallback chain
/// depends on Primary=whisper / Fallback=google / Enable=true wiring so the
/// default deployment runs local Whisper Turbo (no remote API round-trip)
/// and only falls back to Google STT on failure. A silent default drift
/// would flip quick dictation back into a ~7s Google API round-trip.
/// </summary>
public class SpeechProviderSettingsTests
{
    [Fact]
    public void Defaults_MatchProductionFallbackChain()
    {
        var settings = new SpeechProviderSettings();

        Assert.Equal("whisper", settings.PrimaryProvider);
        Assert.Equal("google", settings.FallbackProvider);
        Assert.True(settings.EnableFallback);
    }

    [Fact]
    public void SectionName_IsStableConstant()
    {
        // Binding to "SpeechProvider" is how appsettings.json reaches this
        // type; a rename here would break every deployed config silently.
        Assert.Equal("SpeechProvider", SpeechProviderSettings.SectionName);
    }
}
