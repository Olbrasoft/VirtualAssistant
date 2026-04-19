using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Configuration;

/// <summary>
/// Pins the defaults on SpeechProviderSettings: the whole fallback chain
/// depends on Primary=google / Fallback=whisper / Enable=true wiring so
/// production starts with Google STT and degrades to local Whisper on API
/// errors. A silent default drift would flip the production behavior.
/// </summary>
public class SpeechProviderSettingsTests
{
    [Fact]
    public void Defaults_MatchProductionFallbackChain()
    {
        var settings = new SpeechProviderSettings();

        Assert.Equal("google", settings.PrimaryProvider);
        Assert.Equal("whisper", settings.FallbackProvider);
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
