using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Clipboard;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Processes;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Filters;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Services.EchoDetection;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data;
using VirtualAssistant.Data.EntityFrameworkCore;
using OpenCode.DotnetClient;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering voice processing services (VAD, transcription, audio capture).
/// </summary>
public static class VoiceServicesExtensions
{
    /// <summary>
    /// Adds voice processing services including audio capture, transcription, and LLM integration.
    /// </summary>
    public static IServiceCollection AddVoiceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Claude dispatch service for headless mode execution
        services.AddSingleton<IClaudeDispatchService, ClaudeDispatchService>();

        // String similarity for echo cancellation
        services.AddSingleton<IStringSimilarity, LevenshteinSimilarity>();

        // Echo detection configuration (Strategy pattern for echo cancellation)
        services.Configure<EchoDetectionOptions>(
            configuration.GetSection(nameof(EchoDetectionOptions)));

        // Echo detection support services
        services.AddSingleton<TtsHistoryTracker>();
        services.AddSingleton<TextNormalizer>();
        services.AddSingleton<SimilarityCalculator>();

        // Echo detection strategies (Strategy pattern)
        services.AddSingleton<IEchoDetectionStrategy, ExactMatchStrategy>();
        services.AddSingleton<IEchoDetectionStrategy, PrefixMatchStrategy>();
        services.AddSingleton<IEchoDetectionStrategy, SimilarityMatchStrategy>();

        // Assistant speech tracker for echo cancellation (orchestrates strategies)
        services.AddSingleton<IAssistantSpeechTrackerService, AssistantSpeechTrackerService>();

        // Silero VAD model
        services.AddSingleton<SileroVadOnnxModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new SileroVadOnnxModel(options.Value.SileroVadModelPath);
        });

        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        // Note: IAudioRecordingCoordinator is NOT registered as singleton here
        // because DictationWorker uses a dedicated instance (manually created in WorkerServicesExtensions)
        services.AddSingleton<IVadService, VadService>();

        // Register WhisperSpeechTranscriber (inline Whisper.net - replaced gRPC microservice)
        // Uses ContinuousListenerOptions for configuration (WhisperModelPath, WhisperLanguage, UseGpu)
        services.AddSingleton<ISpeechTranscriber, WhisperSpeechTranscriber>();

        // TranscriptionService with LLM correction and text filtering
        services.AddSingleton<ITranscriptionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TranscriptionService>>();
            var transcriber = sp.GetRequiredService<ISpeechTranscriber>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var textFilter = sp.GetRequiredService<ITextFilter>();
            var llmProvider = sp.GetRequiredService<ILlmProvider>();

            return new TranscriptionService(logger, transcriber, configuration, textFilter, llmProvider);
        });

        // Repeat text intent detection service (for PTT history feature)
        services.AddSingleton<IRepeatTextIntentService, RepeatTextIntentService>();

        // Text input service for OpenCode
        var openCodeUrl = configuration["OpenCodeUrl"] ?? "http://localhost:4096";
        services.AddSingleton<OpenCodeClient>(_ => new OpenCodeClient(openCodeUrl));
        services.AddSingleton<ITextInputService, TextInputService>();

        // Keyboard simulation service for dictation (clipboard-based with dotool)
        services.AddSingleton<IClipboardManager, WlClipboardManager>();
        services.AddSingleton<ITerminalDetector, WaylandTerminalDetector>();
        services.AddSingleton<IKeyboardSimulationService, XDoToolKeyboardService>();

        // Typing sound player for dictation feedback (keyed service)
        services.AddKeyedSingleton<ISoundEffectPlayer, TypingSoundPlayer>("typing", (sp, key) =>
        {
            var logger = sp.GetRequiredService<ILogger<TypingSoundPlayer>>();
            var processExecutor = sp.GetRequiredService<IProcessExecutor>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var soundsPath = Path.Combine(AppContext.BaseDirectory, "..", "sounds");
            var audioSink = configuration["NotificationAudio:AudioSink"];
            return TypingSoundPlayer.CreateFromDirectory(logger, processExecutor, soundsPath, "write.mp3", audioSink);
        });

        // Cancel sound player for dictation cancel feedback (keyed service)
        services.AddKeyedSingleton<ISoundEffectPlayer, CancelSoundPlayer>("cancel", (sp, key) =>
        {
            var logger = sp.GetRequiredService<ILogger<CancelSoundPlayer>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var soundsPath = Path.Combine(AppContext.BaseDirectory, "..", "sounds");
            var audioSink = configuration["NotificationAudio:AudioSink"];
            return CancelSoundPlayer.CreateFromDirectory(logger, soundsPath, "paper-rip.mp3", audioSink);
        });

        // Mute service (shared between tray, keyboard monitor, and continuous listener)
        services.AddSingleton<IManualMuteService, ManualMuteService>();

        // Voice state machine (extracted from ContinuousListenerWorker)
        services.AddSingleton<IVoiceStateMachine>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VoiceStateMachine>>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new VoiceStateMachine(logger, options.Value.StartMuted);
        });

        // Dictation state machine (for keyboard-triggered dictation - Phase 5)
        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine, Olbrasoft.VirtualAssistant.Voice.StateMachine.DictationStateMachine>();

        // Speech buffer manager (extracted from ContinuousListenerWorker)
        services.AddSingleton<ISpeechBufferManager, SpeechBufferManager>();

        // Command detection service (extracted from ContinuousListenerWorker)
        services.AddSingleton<ICommandDetectionService, CommandDetectionService>();

        // External service client (extracted from ContinuousListenerWorker)
        services.AddSingleton<IExternalServiceClient, ExternalServiceClient>();

        // EventBus for worker communication (issue #332)
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        // Keyboard LED reader (for Caps Lock, Scroll Lock, Num Lock)
        services.AddSingleton<IKeyboardLedReader, LinuxKeyboardLedReader>();

        // Keyboard device discovery (finds /dev/input/eventX device)
        services.AddSingleton<IKeyboardDeviceDiscovery, LinuxKeyboardDeviceDiscovery>();

        // Keyboard monitor
        services.AddSingleton<IKeyboardMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EvdevKeyboardMonitor>>();
            var ledReader = sp.GetRequiredService<IKeyboardLedReader>();
            var deviceDiscovery = sp.GetRequiredService<IKeyboardDeviceDiscovery>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new EvdevKeyboardMonitor(logger, ledReader, deviceDiscovery, options.Value.KeyboardDevice);
        });

        // Mistral LLM for Czech ASR correction (Phase 2 - from PushToTalk)
        services.Configure<Olbrasoft.VirtualAssistant.Voice.Configuration.MistralOptions>(
            configuration.GetSection(Olbrasoft.VirtualAssistant.Voice.Configuration.MistralOptions.SectionName));

        // Prompt cache for Mistral (wraps IPromptLoader with reload capability)
        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Services.IPromptCache>(sp =>
        {
            var loader = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.Services.IPromptLoader>();
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Services.ReloadablePromptCache>>();
            return new Olbrasoft.VirtualAssistant.Voice.Services.ReloadablePromptCache(loader, logger);
        });

        // Register MistralProvider as SINGLETON (not transient) so toggle state is shared across all consumers
        services.AddHttpClient("Mistral");
        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Services.ILlmProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Mistral");
            var options = sp.GetRequiredService<IOptions<Olbrasoft.VirtualAssistant.Voice.Configuration.MistralOptions>>();
            var promptCache = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.Services.IPromptCache>();
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Services.MistralProvider>>();

            return new Olbrasoft.VirtualAssistant.Voice.Services.MistralProvider(httpClient, options, promptCache, logger);
        });

        // Transcription corrections repository (used by DatabaseCorrectionFilterStrategy)
        services.AddScoped<ITranscriptionCorrectionRepository, TranscriptionCorrectionRepository>();

        // Text filtering pipeline (Phase 3 - from PushToTalk)
        // Strategy pattern filters registered individually
        var textFiltersConfigPath = Path.Combine(AppContext.BaseDirectory, "Filters", "text-filters.json");

        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Filters.DatabaseCorrectionFilterStrategy>>();
            var scopeFactory = sp.GetService<IServiceScopeFactory>();
            return new Olbrasoft.VirtualAssistant.Voice.Filters.DatabaseCorrectionFilterStrategy(logger, scopeFactory);
        });

        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Filters.FileReplacementFilterStrategy>>();
            return new Olbrasoft.VirtualAssistant.Voice.Filters.FileReplacementFilterStrategy(logger, textFiltersConfigPath);
        });

        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Filters.RemovePatternsFilterStrategy>>();
            return new Olbrasoft.VirtualAssistant.Voice.Filters.RemovePatternsFilterStrategy(logger, textFiltersConfigPath);
        });

        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Filters.WhitespaceFilterStrategy>>();
            return new Olbrasoft.VirtualAssistant.Voice.Filters.WhitespaceFilterStrategy(logger);
        });

        // Composite filter orchestrates all strategies
        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilter, Olbrasoft.VirtualAssistant.Voice.Filters.CompositeTextFilter>();

        // TTS profile resolver for application-specific voice configuration (Issue #405)
        services.Configure<TtsProfilesOptions>(
            configuration.GetSection(TtsProfilesOptions.SectionName));
        services.AddSingleton<ITtsProfileResolver, TtsProfileResolver>();

        return services;
    }
}
