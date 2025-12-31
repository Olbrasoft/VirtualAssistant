extern alias EdgeTtsWebSocket;

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
using Olbrasoft.TextToSpeech.Orchestration.Configuration;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.Azure;
using Olbrasoft.TextToSpeech.Providers.Google;
using Olbrasoft.TextToSpeech.Providers.VoiceRss;
using Olbrasoft.TextToSpeech.Providers.Piper;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using EdgeTtsConfiguration = EdgeTtsWebSocket::Olbrasoft.TextToSpeech.Providers.EdgeTTS.EdgeTtsConfiguration;

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
        // CRITICAL: Configure all provider settings BEFORE registering providers!
        services.Configure<OutputConfiguration>(
            configuration.GetSection(OutputConfiguration.SectionName));
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

        // Register TTS providers inline (Azure, EdgeTTS-HTTP, VoiceRSS, Google)
        // IMPORTANT: Providers must be registered AFTER configuration AND BEFORE orchestration!
        services.AddTtsProviders(configuration);

        // Register EdgeTTS-WebSocket provider (separate package, not included in AddTtsProviders)
        // This MUST be registered AFTER configuration binding
        services.AddSingleton<ITtsProvider, EdgeTtsWebSocket::Olbrasoft.TextToSpeech.Providers.EdgeTTS.EdgeTtsProvider>();

        // Register Piper provider (separate package)
        services.AddPiperTts(configuration);

        // Configure orchestration settings BEFORE registering orchestration
        // This is CRITICAL - AddTtsOrchestration expects this to be called first
        services.Configure<OrchestrationConfig>(
            configuration.GetSection(OrchestrationConfig.SectionName));

        // Register TTS orchestration with circuit breaker and fallback chain
        // This MUST be called AFTER providers are registered AND after Configure<OrchestrationConfig>
        services.AddTtsOrchestration(configuration);

        // Adapter that bridges VirtualAssistant's ITtsProviderChain with the library
        // This MUST be called AFTER orchestration
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
        services.AddScoped<INotificationTracker, NotificationTracker>();
        services.AddSingleton<INotificationBatchingService, NotificationBatchingService>();

        // Speech queue with cancellation support
        services.AddSingleton<ISpeechQueueService, SpeechQueueService>();

        return services;
    }
}
