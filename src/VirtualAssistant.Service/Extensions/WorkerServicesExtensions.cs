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
            var typingSound = sp.GetRequiredService<TypingSoundPlayer>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // Create dedicated AudioCaptureService for dictation (independent from continuous listening)
            var audioCaptureLogger = sp.GetRequiredService<ILogger<AudioCaptureService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var audioCaptureService = new AudioCaptureService(audioCaptureLogger, configuration);

            // Create dedicated TranscriptionService for Dictation with large-v3-turbo model
            var dictationOptions = sp.GetRequiredService<IOptions<DictationOptions>>().Value;

            var transcriberLogger = sp.GetRequiredService<ILogger<SpeechToTextGrpcClient>>();
            var dictationTranscriber = new SpeechToTextGrpcClient(
                transcriberLogger,
                dictationOptions.WhisperLanguage,
                dictationOptions.WhisperModelPath); // Pass model to override service default

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
                audioCaptureService,
                dictationTranscriptionService,
                keyboardSimulation,
                typingSound,
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
