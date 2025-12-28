using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for background worker services registration.
/// Handles hosted services and background workers.
/// </summary>
public static class BackgroundWorkersExtensions
{
    /// <summary>
    /// Adds background worker services.
    /// </summary>
    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
    {
        services.AddHostedService<KeyboardMonitorWorker>();

        // Dictation worker (keyboard-triggered dictation)
        // Uses dedicated AudioCaptureService and TranscriptionService instances
        // Register as singleton first so it can be injected into TrayService
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DictationWorker>>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var stateMachine = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine>();
            var keyboardSimulation = sp.GetRequiredService<IKeyboardSimulationService>();
            var typingSound = sp.GetRequiredService<TypingSoundPlayer>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // Create dedicated AudioCaptureService for dictation
            var audioCaptureLogger = sp.GetRequiredService<ILogger<AudioCaptureService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var audioCaptureService = new AudioCaptureService(audioCaptureLogger, configuration);

            // Create dedicated TranscriptionService for Dictation with large-v3-turbo model
            var dictationOptions = new Olbrasoft.VirtualAssistant.Core.Configuration.DictationOptions();
            configuration.GetSection(Olbrasoft.VirtualAssistant.Core.Configuration.DictationOptions.SectionName).Bind(dictationOptions);

            var transcriberLogger = sp.GetRequiredService<ILogger<SpeechToTextGrpcClient>>();
            var dictationTranscriber = new SpeechToTextGrpcClient(
                transcriberLogger,
                dictationOptions.WhisperLanguage,
                dictationOptions.WhisperModelPath);

            var transcriptionLogger = sp.GetRequiredService<ILogger<TranscriptionService>>();
            var textFilter = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilter>();
            var llmProvider = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.Services.ILlmProvider>();

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

        // Startup notification
        services.AddHostedService<StartupNotificationService>();

        return services;
    }
}
