using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.NotificationAudio.Providers.Linux;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;
using VirtualAssistant.LlmChain;
using Olbrasoft.TextToSpeech.Providers.Extensions;
using Olbrasoft.TextToSpeech.Providers.Piper.Extensions;
using Olbrasoft.TextToSpeech.Orchestration.Extensions;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.Azure;
using Olbrasoft.TextToSpeech.Providers.VoiceRss;
using Olbrasoft.TextToSpeech.Providers.Google;
using Olbrasoft.TextToSpeech.Providers.EdgeTTS;
using Olbrasoft.TextToSpeech.Providers.Piper;
using Olbrasoft.TextToSpeech.Orchestration.Configuration;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering TTS (Text-to-Speech) services.
/// </summary>
public static class TtsServicesExtensions
{
    /// <summary>
    /// Adds TTS services with provider chain and audio playback.
    /// </summary>
    public static IServiceCollection AddTtsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // System paths configuration (lock files, cache directories)
        services.Configure<SystemPathsOptions>(
            configuration.GetSection(SystemPathsOptions.SectionName));

        // Voice profiles configuration (still needed for VoiceConfig mapping)
        services.Configure<TtsVoiceProfilesOptions>(
            configuration.GetSection(TtsVoiceProfilesOptions.SectionName));

        // ========== Inline TTS Providers (Issue #406) ==========
        // Configure TTS provider settings from appsettings.json
        services.Configure<AzureTtsConfiguration>(
            configuration.GetSection(AzureTtsConfiguration.SectionName));
        services.Configure<EdgeTtsConfiguration>(
            configuration.GetSection(EdgeTtsConfiguration.SectionName));
        services.Configure<VoiceRssConfiguration>(
            configuration.GetSection(VoiceRssConfiguration.SectionName));
        services.Configure<GoogleTtsConfiguration>(
            configuration.GetSection(GoogleTtsConfiguration.SectionName));
        services.Configure<PiperConfiguration>(
            configuration.GetSection(PiperConfiguration.SectionName));
        services.Configure<OutputConfiguration>(
            configuration.GetSection(OutputConfiguration.SectionName));
        services.Configure<OrchestrationConfig>(
            configuration.GetSection(OrchestrationConfig.SectionName));

        // Register TTS providers inline (Azure, EdgeTTS, VoiceRSS, Google)
        services.AddTtsProviders(configuration);

        // Register Piper provider (separate package)
        services.AddPiperTts(configuration);

        // Register TTS orchestration with circuit breaker and fallback chain
        services.AddTtsOrchestration(configuration);
        // =======================================================

        // TTS focused services (SRP compliant)
        // SpeechToText client for querying recording status
        services.Configure<SpeechToTextSettings>(configuration.GetSection(SpeechToTextSettings.SectionName));
        services.AddHttpClient<ISpeechToTextClient, SpeechToTextClient>(client =>
        {
            var settings = configuration.GetSection(SpeechToTextSettings.SectionName).Get<SpeechToTextSettings>() ?? new SpeechToTextSettings();
            client.Timeout = TimeSpan.FromMilliseconds(settings.StatusTimeoutMs);
        });

        // SpeechLockService for backward compatibility with speech-lock API
        services.AddSingleton<ISpeechLockService, SpeechLockService>();
        services.AddSingleton<ITtsQueueService, TtsQueueService>();
        services.AddSingleton<ITtsCacheService, TtsCacheService>();

        // NotificationAudio - priority-based audio playback (PipeWire → PulseAudio → FFmpeg)
        services.AddNotificationAudio();

        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<TtsService>();

        // Workspace detection for smart TTS notifications
        services.AddSingleton<IWorkspaceDetectionService, WorkspaceDetectionService>();

        // VirtualAssistantSpeaker - single entry point for all TTS operations
        services.AddSingleton<IVirtualAssistantSpeaker, VirtualAssistantSpeaker>();

        // LLM Chain for multi-provider fallback
        services.AddLlmChain(configuration);

        // Notification humanization and batching services
        services.AddSingleton<IHumanizationService, HumanizationService>();
        services.AddSingleton<INotificationBatchingService, NotificationBatchingService>();

        // Speech queue with cancellation support
        services.AddSingleton<ISpeechQueueService, SpeechQueueService>();

        return services;
    }
}
