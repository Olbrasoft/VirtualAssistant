using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Microsoft.AspNetCore.SignalR;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Service.Workers.Streaming;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Filters;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering background worker services.
/// </summary>
public static class WorkerServicesExtensions
{
    /// <summary>
    /// Adds background worker services (hosted services).
    /// </summary>
    private const string DictationKey = "dictation";

    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        services.AddHostedService<KeyboardMonitorWorker>();

        // Dictation has its own audio capture + recording coordinator + Whisper
        // instance so it never contends with the continuous-listening pipeline.
        // Each dependency gets its own keyed Singleton registration — the
        // DictationWorker factory below then pulls them from the container
        // instead of new-ing them inline.
        services.AddDictationAudioServices();
        services.AddDictationTranscriberServices();

        services.AddSingleton(sp =>
        {
            // Streaming assembler is created inline so it binds to the SAME
            // keyed ITranscriptionService the worker uses — otherwise a
            // second (non-keyed) transcription instance would be resolved
            // for per-chunk calls. (#969 extraction.)
            var transcription = sp.GetRequiredKeyedService<ITranscriptionService>(DictationKey);
            var assembler = new StreamingChunkAssembler(
                sp.GetRequiredService<ILogger<StreamingChunkAssembler>>(),
                transcription);

            return new DictationWorker(
                sp.GetRequiredService<ILogger<DictationWorker>>(),
                sp.GetRequiredService<IKeyboardMonitor>(),
                sp.GetRequiredService<Voice.StateMachine.IDictationStateMachine>(),
                sp.GetRequiredKeyedService<IAudioRecordingCoordinator>(DictationKey),
                transcription,
                sp.GetRequiredService<IKeyboardSimulationService>(),
                sp.GetRequiredKeyedService<ISoundEffectPlayer>("typing"),
                sp.GetRequiredKeyedService<ISoundEffectPlayer>("cancel"),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IHubContext<DictationHub>>(),
                sp.GetRequiredService<ICliAppDetector>(),
                assembler,
                sp.GetRequiredService<IOptions<DictationOptions>>());
        });

        // Register the same instance as IDictationControl interface (issue #466)
        // This allows MenuEventDispatcher to control dictation functionality
        services.AddSingleton<IDictationControl>(sp => sp.GetRequiredService<DictationWorker>());

        // Register the same instance as IDictationService interface (issue #846)
        // This allows DictationHub to control dictation from web remote
        services.AddSingleton<IDictationService>(sp => sp.GetRequiredService<DictationWorker>());

        // Register the same singleton instance as hosted service
        services.AddHostedService(sp => sp.GetRequiredService<DictationWorker>());

        // Dictation-TTS coordination (prevents TTS during dictation)
        services.AddHostedService<DictationSpeechCoordinator>();

        // Startup notification (Phase 1: simple "System started")
        services.AddHostedService<StartupNotificationService>();

        // Desktop Monitor broadcasting (SignalR hub updates)
        services.AddHostedService<DesktopMonitorBroadcastWorker>();

        // Screenshot watcher (instant SignalR broadcast when new screenshot appears)
        services.AddHostedService<ScreenshotWatcherWorker>();

        return services;
    }

    /// <summary>
    /// Registers the dedicated audio-capture and recording-coordinator instances
    /// used by dictation. Keyed so they stay isolated from the continuous-listening
    /// pipeline's shared singletons.
    /// </summary>
    private static void AddDictationAudioServices(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IAudioCaptureService>(DictationKey, (sp, _) =>
            new AudioCaptureService(
                sp.GetRequiredService<ILogger<AudioCaptureService>>(),
                sp.GetRequiredService<IConfiguration>()));

        // Register under the interface so callers don't need to know the concrete
        // implementation. Keeps DictationWorker and any future consumer decoupled
        // from Voice.Audio.AudioRecordingCoordinator specifically.
        //
        // The buffer manager and scheduler are constructed inline rather than as
        // separate top-level registrations because their state is per-coordinator —
        // registering them as global singletons would share buffer/cursor state
        // across any future second coordinator (e.g. the continuous-listening
        // pipeline, if it ever grows a streaming component).
        services.AddKeyedSingleton<IAudioRecordingCoordinator>(DictationKey, (sp, _) =>
            new Voice.Audio.AudioRecordingCoordinator(
                sp.GetRequiredService<ILogger<Voice.Audio.AudioRecordingCoordinator>>(),
                sp.GetRequiredKeyedService<IAudioCaptureService>(DictationKey),
                sp.GetRequiredService<IOptions<Voice.Configuration.AudioRecordingOptions>>(),
                new Voice.Audio.AudioBufferManager(),
                new Voice.Audio.ChunkEmissionScheduler(sp.GetRequiredService<ILogger<Voice.Audio.ChunkEmissionScheduler>>())));
    }

    /// <summary>
    /// Registers the dedicated Whisper / transcriber chain and the TranscriptionService
    /// that the dictation pipeline consumes. Keyed so they stay isolated from the
    /// continuous-listening pipeline; each composition step is its own Singleton
    /// registration so the graph is flat and individually testable.
    /// </summary>
    private static void AddDictationTranscriberServices(this IServiceCollection services)
    {
        // Dedicated Whisper tuned with DictationOptions (large-v3-turbo, GPU).
        services.AddKeyedSingleton(DictationKey, (sp, _) =>
        {
            var dictationOptions = sp.GetRequiredService<IOptions<DictationOptions>>().Value;
            var continuousOptions = new ContinuousListenerOptions
            {
                WhisperModelPath = dictationOptions.WhisperModelPath,
                WhisperLanguage = dictationOptions.WhisperLanguage,
                UseGpu = true
            };
            return new WhisperSpeechTranscriber(
                sp.GetRequiredService<ILogger<WhisperSpeechTranscriber>>(),
                Microsoft.Extensions.Options.Options.Create(continuousOptions));
        });

        // Active transcriber for dictation — Google primary + dedicated Whisper fallback
        // when configured, otherwise the single available provider.
        // NOTE: GoogleSpeechTranscriber is registered as a concrete singleton in
        // VoiceServicesExtensions (NOT as a keyed ISpeechTranscriber), so we pull it
        // by type here. A previous version of this code used
        // GetKeyedService<ISpeechTranscriber>("google") which always returned null
        // and silently fell back to Whisper (pre-existing bug) — Copilot caught it
        // during the #977 review.
        services.AddKeyedSingleton<ISpeechTranscriber>(DictationKey, (sp, _) =>
        {
            var speechSettings = sp.GetRequiredService<IOptions<SpeechProviderSettings>>().Value;
            var factory = sp.GetRequiredService<ISpeechTranscriberFactory>();
            var googleTranscriber = sp.GetService<GoogleSpeechTranscriber>();
            var whisper = sp.GetRequiredKeyedService<WhisperSpeechTranscriber>(DictationKey);

            if (googleTranscriber == null)
                return whisper;

            if (!speechSettings.EnableFallback)
                return googleTranscriber;

            return new FallbackSpeechTranscriber(
                googleTranscriber,
                whisper,
                factory,
                sp.GetRequiredService<ILogger<FallbackSpeechTranscriber>>(),
                speechSettings);
        });

        services.AddKeyedSingleton<ITranscriptionService>(DictationKey, (sp, _) =>
            new TranscriptionService(
                sp.GetRequiredService<ILogger<TranscriptionService>>(),
                sp.GetRequiredKeyedService<ISpeechTranscriber>(DictationKey),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ITextFilter>(),
                sp.GetRequiredService<ILightweightTextFilter>(),
                sp.GetRequiredService<ILlmProviderFactory>(),
                sp.GetRequiredService<IRacingLlmProvider>(),
                sp.GetRequiredService<IOptions<LlmProviderOptions>>()));
    }
}
