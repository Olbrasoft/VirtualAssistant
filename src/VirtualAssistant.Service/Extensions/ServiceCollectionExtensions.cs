using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using OpenCode.DotnetClient;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub;
using VirtualAssistant.Core;
using VirtualAssistant.Core.Services;
using VirtualAssistant.LlmChain;
// TextToSpeech Library
using Olbrasoft.TextToSpeech.Providers.Extensions;
using Olbrasoft.TextToSpeech.Providers.Piper.Extensions;
using Olbrasoft.TextToSpeech.Orchestration.Extensions;
using LibraryChain = Olbrasoft.TextToSpeech.Orchestration.ITtsProviderChain;

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
        services.AddSingleton<AssistantSpeechTrackerService>();

        // Silero VAD model
        services.AddSingleton<SileroVadOnnxModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new SileroVadOnnxModel(options.Value.SileroVadModelPath);
        });

        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<VadService>();
        services.AddSingleton<WhisperNetTranscriber>();
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

        // ========== TextToSpeech Library Integration ==========
        // IMPORTANT: Configure TTS providers FIRST (hosting app decides where values come from)
        // This is where we load configuration from appsettings.json, environment variables, etc.
        services.ConfigureTtsProviders(configuration);

        // Register providers from the new library (Azure, EdgeTTS, VoiceRSS, Google)
        services.AddTtsProviders(configuration);

        // Register EdgeTTS WebSocket provider (overrides HTTP-based provider during development)
        services.AddEdgeTtsWebSocketProvider(configuration);

        // Register Piper provider (separate due to ONNX dependency)
        services.AddPiperTts(configuration);

        // Register orchestration (circuit breaker, fallback chain)
        services.AddTtsOrchestration(configuration);

        // Adapter that bridges VirtualAssistant's ITtsProviderChain with the library
        services.AddSingleton<ITtsProviderChain, TtsProviderChainAdapter>();
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
        // Prompt loader for LLM routers
        services.AddSingleton<IPromptLoader, PromptLoader>();

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
    /// Adds background worker services.
    /// </summary>
    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
    {
        services.AddHostedService<KeyboardMonitorWorker>();
        services.AddHostedService<ContinuousListenerWorker>();

        // Startup notification (Phase 1: simple "System started")
        services.AddHostedService<StartupNotificationService>();

        return services;
    }
}
