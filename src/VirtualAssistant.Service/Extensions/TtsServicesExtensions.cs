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

        // ========== TextToSpeech.Service HTTP Client ==========
        // VirtualAssistant now delegates all TTS to centralized TextToSpeech.Service (port 5060)
        // No more direct Azure/EdgeTTS/Piper provider integration - everything goes through the service
        services.AddSingleton<ITtsProviderChain, TextToSpeechHttpClient>();
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
