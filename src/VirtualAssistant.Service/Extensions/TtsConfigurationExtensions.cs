using Olbrasoft.TextToSpeech.Orchestration.Configuration;
using Olbrasoft.TextToSpeech.Providers.Azure;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.EdgeTTS;
using Olbrasoft.TextToSpeech.Providers.Google;
using Olbrasoft.TextToSpeech.Providers.Piper;
using Olbrasoft.TextToSpeech.Providers.VoiceRss;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for configuring TTS providers.
/// All secrets are loaded from SecureStore via IConfiguration.
/// </summary>
public static class TtsConfigurationExtensions
{
    /// <summary>
    /// Configures all TTS providers with values from IConfiguration (appsettings.json + SecureStore).
    /// Secrets are loaded from SecureStore, not environment variables.
    /// </summary>
    public static IServiceCollection ConfigureTtsProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Output configuration (determines if audio is saved to file, returned in memory, or both)
        services.Configure<OutputConfiguration>(
            configuration.GetSection(OutputConfiguration.SectionName));

        // EdgeTTS HTTP provider configuration
        services.Configure<EdgeTtsConfiguration>(
            configuration.GetSection(EdgeTtsConfiguration.SectionName));

        // Azure Cognitive Services TTS configuration
        // SubscriptionKey loaded from SecureStore: TTS:AzureTTS:SubscriptionKey
        services.Configure<AzureTtsConfiguration>(
            configuration.GetSection(AzureTtsConfiguration.SectionName));

        // VoiceRSS TTS configuration
        // ApiKey loaded from SecureStore: TTS:VoiceRSS:ApiKey
        services.Configure<VoiceRssConfiguration>(
            configuration.GetSection(VoiceRssConfiguration.SectionName));

        // Google TTS configuration
        services.Configure<GoogleTtsConfiguration>(
            configuration.GetSection(GoogleTtsConfiguration.SectionName));

        // Piper TTS configuration
        services.Configure<PiperConfiguration>(
            configuration.GetSection(PiperConfiguration.SectionName));

        // Orchestration configuration (circuit breaker, fallback chain)
        services.Configure<OrchestrationConfig>(
            configuration.GetSection(OrchestrationConfig.SectionName));

        return services;
    }
}
