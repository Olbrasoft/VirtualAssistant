using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using Olbrasoft.VirtualAssistant.Voice.Workers;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Workers;
using OpenCode.DotnetClient;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub;
using VirtualAssistant.Core;
using VirtualAssistant.Core.Services;
using VirtualAssistant.LlmChain;
// NotificationAudio Library
using Olbrasoft.NotificationAudio.Providers.Linux;
// SystemTray Library
using Olbrasoft.SystemTray.Linux;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for service collection configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all VirtualAssistant services to the service collection.
    /// </summary>
    public static IServiceCollection AddVirtualAssistantServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddCoreConfiguration(configuration)
            .AddDataServices(configuration)
            .AddVoiceServices(configuration)
            .AddTtsServices(configuration)
            .AddLlmServices(configuration)
            .AddTrayServices()
            .AddBackgroundWorkers()
            .AddControllers();

        return services;
    }

    /// <summary>
    /// Adds core configuration options.
    /// </summary>
    public static IServiceCollection AddCoreConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ContinuousListenerOptions>(
            configuration.GetSection(ContinuousListenerOptions.SectionName));

        services.Configure<ClaudeDispatchOptions>(
            configuration.GetSection(ClaudeDispatchOptions.SectionName));

        services.Configure<ExternalServicesOptions>(
            configuration.GetSection(ExternalServicesOptions.SectionName));

        // Dependent services manager (manages TextToSpeech.Service lifecycle)
        services.AddSingleton<IDependentServiceManager, DependentServicesManager>();

        return services;
    }

    /// <summary>
    /// Adds data layer services (EF Core, CQRS handlers).
    /// </summary>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("VirtualAssistantDb")
            ?? throw new InvalidOperationException("Connection string 'VirtualAssistantDb' not found.");
        services.AddVirtualAssistantData(connectionString);

        // GitHub sync services
        services.AddGitHubServices(configuration);

        // Core services (AgentHubService)
        services.AddCoreServices();

        return services;
    }

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
        services.AddSingleton<IAssistantSpeechTrackerService, AssistantSpeechTrackerService>();

        // Silero VAD model
        services.AddSingleton<SileroVadOnnxModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new SileroVadOnnxModel(options.Value.SileroVadModelPath);
        });

        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IVadService, VadService>();
        // Use SpeechToText gRPC microservice instead of local Whisper.net
        services.AddSingleton<ISpeechTranscriber, SpeechToTextGrpcClient>();
        services.AddSingleton<ITranscriptionService, TranscriptionService>();

        // Repeat text intent detection service (for PTT history feature)
        services.AddSingleton<IRepeatTextIntentService, RepeatTextIntentService>();

        // Text input service for OpenCode
        var openCodeUrl = configuration["OpenCodeUrl"] ?? "http://localhost:4096";
        services.AddSingleton(new OpenCodeClient(openCodeUrl));
        services.AddSingleton<ITextInputService, TextInputService>();

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

        // Keyboard monitor
        services.AddSingleton<IKeyboardMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EvdevKeyboardMonitor>>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new EvdevKeyboardMonitor(logger, options.Value.KeyboardDevice);
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

        services.AddHttpClient<Olbrasoft.VirtualAssistant.Voice.Services.ILlmProvider, Olbrasoft.VirtualAssistant.Voice.Services.MistralProvider>();

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

        return services;
    }

    /// <summary>
    /// Adds TTS services with provider chain.
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

    /// <summary>
    /// Adds LLM router services.
    /// </summary>
    public static IServiceCollection AddLlmServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Prompt loader for LLM routers - upgraded to HybridPromptLoader (file + embedded resource fallback)
        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Services.IPromptLoader>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Services.HybridPromptLoader>>();
            var embeddedLoader = new Olbrasoft.VirtualAssistant.Voice.Services.EmbeddedPromptLoader();
            var promptsPath = Path.Combine(AppContext.BaseDirectory, "Prompts");

            return new Olbrasoft.VirtualAssistant.Voice.Services.HybridPromptLoader(
                promptsPath,
                embeddedLoader,
                logger);
        });

        // HttpClient
        services.AddHttpClient();

        // LLM Routers - register as BaseLlmRouterService for MultiProvider to collect
        services.AddSingleton<BaseLlmRouterService, CerebrasRouterService>();
        services.AddSingleton<BaseLlmRouterService, GroqRouterService>();
        services.AddSingleton<BaseLlmRouterService, MistralRouterService>();
        services.AddSingleton<ILlmRouterService, MultiProviderLlmRouter>();

        return services;
    }

    /// <summary>
    /// Adds tray icon services.
    /// </summary>
    public static IServiceCollection AddTrayServices(this IServiceCollection services)
    {
        // Icon renderer for SVG rendering
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IconRenderer>>();
            return new IconRenderer(logger);
        });

        // Tray icon manager for managing tray icons
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TrayIconManager>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var iconRenderer = sp.GetRequiredService<IconRenderer>();
            return new TrayIconManager(logger, loggerFactory, iconRenderer);
        });

        // D-Bus menu handler for tray icon context menu
        services.AddSingleton<ITrayMenuHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantDBusMenuHandler>>();
            return new VirtualAssistantDBusMenuHandler(logger);
        });

        // SpeechToText service manager for controlling SpeechToText microservice
        services.AddSingleton<SpeechToTextServiceManager>();

        // VirtualAssistant tray service (wrapper for tray functionality)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantTrayService>>();
            var manager = sp.GetRequiredService<TrayIconManager>();
            var muteService = sp.GetRequiredService<IManualMuteService>();
            var dependentServiceManager = sp.GetRequiredService<IDependentServiceManager>();
            var menuHandler = sp.GetRequiredService<ITrayMenuHandler>();
            var sttServiceManager = sp.GetRequiredService<SpeechToTextServiceManager>();
            var mistralProvider = sp.GetService<Olbrasoft.VirtualAssistant.Voice.Services.MistralProvider>();
            var dictationStateMachine = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            var iconsPath = Path.Combine(AppContext.BaseDirectory, "icons");

            return new VirtualAssistantTrayService(
                logger,
                manager,
                muteService,
                dependentServiceManager,
                iconsPath,
                options.Value.LogViewerPort,
                menuHandler,
                sttServiceManager,
                mistralProvider,
                dictationStateMachine);
        });

        return services;
    }

    /// <summary>
    /// Adds background worker services.
    /// </summary>
    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
    {
        services.AddHostedService<KeyboardMonitorWorker>();

        // New event-driven workers (issue #332 - SOLID refactoring)
        // NOTE: These workers are for continuous listening mode (currently muted by default)
        services.AddHostedService<AudioCapturerWorker>();
        services.AddHostedService<VoiceActivityWorker>();
        services.AddHostedService<TranscriptionRouterWorker>();
        services.AddHostedService<ActionExecutorWorker>();

        // Dictation worker (Phase 5 - keyboard-triggered dictation)
        // Uses dedicated AudioCaptureService instance (not shared with continuous listening)
        services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DictationWorker>>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var stateMachine = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine>();
            var transcriptionService = sp.GetRequiredService<ITranscriptionService>();
            var textInputService = sp.GetRequiredService<ITextInputService>();

            // Create dedicated AudioCaptureService for dictation (independent from continuous listening)
            var audioCaptureLogger = sp.GetRequiredService<ILogger<AudioCaptureService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var audioCaptureService = new AudioCaptureService(audioCaptureLogger, configuration);

            return new DictationWorker(
                logger,
                keyboardMonitor,
                stateMachine,
                audioCaptureService,
                transcriptionService,
                textInputService);
        });

        // TODO: Remove after testing new workers
        // services.AddHostedService<ContinuousListenerWorker>();

        // Startup notification (Phase 1: simple "System started")
        services.AddHostedService<StartupNotificationService>();

        return services;
    }
}
