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
            var audioRecordingOptions = sp.GetRequiredService<IOptions<Olbrasoft.VirtualAssistant.Voice.Configuration.AudioRecordingOptions>>();
            var recordingCoordinator = new Olbrasoft.VirtualAssistant.Voice.Audio.AudioRecordingCoordinator(
                coordinatorLogger,
                audioCaptureService,
                audioRecordingOptions);

            // Create dedicated TranscriptionService for Dictation with large-v3-turbo model
            var dictationOptionsService = sp.GetRequiredService<IOptions<DictationOptions>>();
            var dictationOptions = dictationOptionsService.Value;

            // Create dedicated WhisperSpeechTranscriber for DictationWorker
            // Uses DictationOptions.WhisperModelPath (large-v3-turbo) instead of ContinuousListener model
            var continuousOptions = new ContinuousListenerOptions
            {
                WhisperModelPath = dictationOptions.WhisperModelPath,
                WhisperLanguage = dictationOptions.WhisperLanguage,
                UseGpu = true // Enable GPU acceleration for dictation
            };
            var dictationLogger = sp.GetRequiredService<ILogger<WhisperSpeechTranscriber>>();
            var dictationTranscriber = new WhisperSpeechTranscriber(
                dictationLogger,
                Microsoft.Extensions.Options.Options.Create(continuousOptions));

            var transcriptionLogger = sp.GetRequiredService<ILogger<TranscriptionService>>();
            var textFilter = sp.GetRequiredService<ITextFilter>();
            var llmProvider = sp.GetRequiredService<ILlmProvider>();

            var dictationTranscriptionService = new TranscriptionService(
                transcriptionLogger,
                dictationTranscriber,
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
                scopeFactory,
                dictationOptionsService);
        });

        // Register the same instance as IDictationControl interface (issue #466)
        // This allows MenuEventDispatcher to control dictation functionality
        services.AddSingleton<IDictationControl>(sp => sp.GetRequiredService<DictationWorker>());

        // Register the same singleton instance as hosted service
        services.AddHostedService(sp => sp.GetRequiredService<DictationWorker>());

        // Dictation-TTS coordination (prevents TTS during dictation)
        services.AddHostedService<DictationSpeechCoordinator>();

        // Startup notification (Phase 1: simple "System started")
        services.AddHostedService<StartupNotificationService>();

        // Desktop Monitor broadcasting (SignalR hub updates)
        services.AddHostedService<DesktopMonitorBroadcastWorker>();

        return services;
    }
}
