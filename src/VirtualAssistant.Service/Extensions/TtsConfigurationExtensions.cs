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
/// This is where the hosting application decides how to load configuration values
/// (from appsettings.json, environment variables, database, Key Vault, etc.).
/// </summary>
public static class TtsConfigurationExtensions
{
    /// <summary>
    /// Configures all TTS providers with values from appsettings.json and environment variables.
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
        services.Configure<AzureTtsConfiguration>(options =>
        {
            var section = configuration.GetSection(AzureTtsConfiguration.SectionName);
            section.Bind(options);

            // Load secrets from environment variables (hosting app responsibility)
            var envKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                options.SubscriptionKey = envKey;
            }

            var envRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");
            if (!string.IsNullOrEmpty(envRegion))
            {
                options.Region = envRegion;
            }
        });

        // VoiceRSS TTS configuration
        services.Configure<VoiceRssConfiguration>(options =>
        {
            var section = configuration.GetSection(VoiceRssConfiguration.SectionName);
            section.Bind(options);

            // Load API key from environment variables (hosting app responsibility)
            var envKey = Environment.GetEnvironmentVariable("VOICERSS_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                options.ApiKey = envKey;
            }
        });

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
