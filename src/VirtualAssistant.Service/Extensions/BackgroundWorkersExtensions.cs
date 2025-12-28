using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Factories;
using Olbrasoft.VirtualAssistant.Voice.Audio;

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

        // Register DictationServicesFactory for creating dictation-specific services
        services.AddSingleton<DictationServicesFactory>();

        // Dictation worker (keyboard-triggered dictation)
        // Uses factory to create dedicated service instances with dictation configuration
        // Register as singleton first so it can be injected into TrayService
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DictationWorker>>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var stateMachine = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine>();
            var keyboardSimulation = sp.GetRequiredService<IKeyboardSimulationService>();
            var typingSound = sp.GetRequiredService<TypingSoundPlayer>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            // Use factory to create dictation-specific services
            var factory = sp.GetRequiredService<DictationServicesFactory>();
            var audioCaptureService = factory.CreateAudioCaptureService();
            var transcriptionService = factory.CreateTranscriptionService();

            return new DictationWorker(
                logger,
                keyboardMonitor,
                stateMachine,
                audioCaptureService,
                transcriptionService,
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
