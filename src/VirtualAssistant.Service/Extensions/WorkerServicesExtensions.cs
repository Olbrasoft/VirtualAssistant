using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Voice;
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
    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        services.AddHostedService<KeyboardMonitorWorker>();

        // Dictation worker (Phase 5 - keyboard-triggered dictation)
        // Uses dedicated AudioCaptureService and TranscriptionService instances (not shared with continuous listening)
        // Register as singleton first so it can be injected into TrayService
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DictationWorker>>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var stateMachine = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine>();
            var keyboardSimulation = sp.GetRequiredService<IKeyboardSimulationService>();
            var typingSound = sp.GetRequiredKeyedService<ISoundEffectPlayer>("typing");
            var cancelSound = sp.GetRequiredKeyedService<ISoundEffectPlayer>("cancel");
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // Create dedicated AudioCaptureService for dictation (independent from continuous listening)
            var audioCaptureLogger = sp.GetRequiredService<ILogger<AudioCaptureService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var audioCaptureService = new AudioCaptureService(audioCaptureLogger, configuration);

            // Create AudioRecordingCoordinator with dedicated audio capture
            var coordinatorLogger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Audio.AudioRecordingCoordinator>>();
            var recordingCoordinator = new Olbrasoft.VirtualAssistant.Voice.Audio.AudioRecordingCoordinator(
                coordinatorLogger,
                audioCaptureService);

            // TODO: Issue #460/#461 - Restore dictationTranscriber with WhisperNetTranscriber
            // Temporarily using null - DictationWorker will not transcribe until migration is complete
            var dictationOptions = sp.GetRequiredService<IOptions<DictationOptions>>().Value;

            // var transcriberLogger = sp.GetRequiredService<ILogger<SpeechToTextGrpcClient>>();
            // var dictationTranscriber = new SpeechToTextGrpcClient(...);

            var transcriptionLogger = sp.GetRequiredService<ILogger<TranscriptionService>>();
            var textFilter = sp.GetRequiredService<ITextFilter>();
            var llmProvider = sp.GetRequiredService<ILlmProvider>();

            // Use null transcriber temporarily - will be replaced in issue #461
            Core.Speech.ISpeechTranscriber? dictationTranscriber = null;

            var dictationTranscriptionService = new TranscriptionService(
                transcriptionLogger,
                dictationTranscriber!,
                configuration,
                textFilter,
                llmProvider);

            return new DictationWorker(
                logger,
                keyboardMonitor,
                stateMachine,
                recordingCoordinator,
                dictationTranscriptionService,
                keyboardSimulation,
                typingSound,
                cancelSound,
                scopeFactory);
        });

        // Register the same singleton instance as hosted service
        services.AddHostedService(sp => sp.GetRequiredService<DictationWorker>());

        // Dictation-TTS coordination (prevents TTS during dictation)
        services.AddHostedService<DictationSpeechCoordinator>();

        // Startup notification (Phase 1: simple "System started")
        services.AddHostedService<StartupNotificationService>();

        return services;
    }
}
