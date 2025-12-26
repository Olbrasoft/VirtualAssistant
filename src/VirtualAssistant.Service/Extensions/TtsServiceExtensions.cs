using Olbrasoft.NotificationAudio.Providers.Linux;
using Olbrasoft.TextToSpeech.Orchestration.Extensions;
using Olbrasoft.TextToSpeech.Providers.Extensions;
using Olbrasoft.TextToSpeech.Providers.Piper.Extensions;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;
using VirtualAssistant.LlmChain;
using LibraryChain = Olbrasoft.TextToSpeech.Orchestration.ITtsProviderChain;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for TTS services with provider chain.
/// </summary>
public static class TtsServiceExtensions
{
    /// <summary>
    /// Adds TTS services with provider chain.
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

        // ========== TextToSpeech Library Integration ==========
        // IMPORTANT: Configure TTS providers FIRST (hosting app decides where values come from)
        // This is where we load configuration from appsettings.json, environment variables, etc.
        services.ConfigureTtsProviders(configuration);

        // Register providers from the new library (Azure, EdgeTTS, VoiceRSS, Google)
        services.AddTtsProviders(configuration);

        // Register EdgeTTS WebSocket provider (overrides HTTP-based provider during development)
        services.AddEdgeTtsWebSocketProvider(configuration);

        // Register Piper provider (separate due to ONNX dependency)
        services.AddPiperTts(configuration);

        // Register orchestration (circuit breaker, fallback chain)
        services.AddTtsOrchestration(configuration);

        // Adapter that bridges VirtualAssistant's ITtsProviderChain with the library
        services.AddSingleton<ITtsProviderChain, TtsProviderChainAdapter>();
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
