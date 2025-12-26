using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using OpenCode.DotnetClient;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for voice processing services (VAD, transcription, audio capture).
/// </summary>
public static class VoiceServiceExtensions
{
    /// <summary>
    /// Adds voice processing services (VAD, transcription, audio capture).
    /// </summary>
    public static IServiceCollection AddVoiceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Claude dispatch service for headless mode execution
        services.AddSingleton<IClaudeDispatchService, ClaudeDispatchService>();

        // String similarity for echo cancellation
        services.AddSingleton<IStringSimilarity, LevenshteinSimilarity>();

        // Assistant speech tracker for echo cancellation
        services.AddSingleton<AssistantSpeechTrackerService>();

        // Silero VAD model
        services.AddSingleton<SileroVadOnnxModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new SileroVadOnnxModel(options.Value.SileroVadModelPath);
        });

        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<VadService>();
        // Use SpeechToText gRPC microservice instead of local Whisper.net
        services.AddSingleton<ISpeechTranscriber, SpeechToTextGrpcClient>();
        services.AddSingleton<TranscriptionService>();

        // Repeat text intent detection service (for PTT history feature)
        services.AddSingleton<IRepeatTextIntentService, RepeatTextIntentService>();

        // Text input service for OpenCode
        var openCodeUrl = configuration["OpenCodeUrl"] ?? "http://localhost:4096";
        services.AddSingleton(new OpenCodeClient(openCodeUrl));
        services.AddSingleton<TextInputService>();

        // Mute service (shared between tray, keyboard monitor, and continuous listener)
        services.AddSingleton<IManualMuteService, ManualMuteService>();

        // Voice state machine (extracted from ContinuousListenerWorker)
        services.AddSingleton<IVoiceStateMachine>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VoiceStateMachine>>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new VoiceStateMachine(logger, options.Value.StartMuted);
        });

        // Speech buffer manager (extracted from ContinuousListenerWorker)
        services.AddSingleton<ISpeechBufferManager, SpeechBufferManager>();

        // Command detection service (extracted from ContinuousListenerWorker)
        services.AddSingleton<ICommandDetectionService, CommandDetectionService>();

        // External service client (extracted from ContinuousListenerWorker)
        services.AddSingleton<IExternalServiceClient, ExternalServiceClient>();

        // Keyboard monitor
        services.AddSingleton<IKeyboardMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EvdevKeyboardMonitor>>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new EvdevKeyboardMonitor(logger, options.Value.KeyboardDevice);
        });

        return services;
    }
}
