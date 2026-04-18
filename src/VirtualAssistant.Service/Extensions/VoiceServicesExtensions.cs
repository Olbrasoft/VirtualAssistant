using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
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
using Olbrasoft.VirtualAssistant.Service.Infrastructure;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Filters;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Services.EchoDetection;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using OpenCode.DotnetClient;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering voice processing services.
/// </summary>
public static class VoiceServicesExtensions
{
    public static IServiceCollection AddVoiceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddVoiceConfigurationOptions(configuration);
        services.AddEchoDetectionServices();
        services.AddAudioCaptureServices();
        services.AddTranscriptionServices(configuration);
        services.AddKeyboardServices();
        services.AddSoundEffectPlayers();
        services.AddStateMachineServices();
        services.AddVoiceLlmServices(configuration);
        services.AddTextFilterServices();

        return services;
    }

    private static void AddVoiceConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AudioRecordingOptions>(
            configuration.GetSection(AudioRecordingOptions.SectionName));

        services.Configure<EchoDetectionOptions>(
            configuration.GetSection(nameof(EchoDetectionOptions)));

        services.Configure<LlmRoutingOptions>(
            configuration.GetSection(LlmRoutingOptions.SectionName));

        services.Configure<MistralOptions>(
            configuration.GetSection(Olbrasoft.VirtualAssistant.Voice.Configuration.MistralOptions.SectionName));

        services.Configure<ZenOptions>(
            configuration.GetSection(ZenOptions.SectionName));

        services.Configure<MercuryOptions>(
            configuration.GetSection(MercuryOptions.SectionName));

        services.Configure<LlmProviderOptions>(
            configuration.GetSection(LlmProviderOptions.SectionName));

        services.Configure<TtsProfilesOptions>(
            configuration.GetSection(TtsProfilesOptions.SectionName));

        services.Configure<GoogleSpeechToTextOptions>(
            configuration.GetSection(GoogleSpeechToTextOptions.SectionName));

        services.Configure<SpeechProviderSettings>(
            configuration.GetSection(SpeechProviderSettings.SectionName));
    }

    private static void AddEchoDetectionServices(this IServiceCollection services)
    {
        services.AddSingleton<IStringSimilarity, LevenshteinSimilarity>();

        services.AddSingleton<TtsHistoryTracker>();
        services.AddSingleton<TextNormalizer>();
        services.AddSingleton<SimilarityCalculator>();

        services.AddSingleton<IEchoDetectionStrategy, ExactMatchStrategy>();
        services.AddSingleton<IEchoDetectionStrategy, PrefixMatchStrategy>();
        services.AddSingleton<IEchoDetectionStrategy, SimilarityMatchStrategy>();

        services.AddSingleton<IAssistantSpeechTrackerService, AssistantSpeechTrackerService>();
    }

    private static void AddAudioCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new SileroVadOnnxModel(options.Value.SileroVadModelPath);
        });

        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IVadService, VadService>();
    }

    private static void AddTranscriptionServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClaudeOutputParser, ClaudeOutputParser>();
        services.AddSingleton<IClaudeNotificationSender, ClaudeNotificationSender>();
        services.AddSingleton<IClaudeDispatchService, ClaudeDispatchService>();

        // Register STT providers as concrete singletons (no keyed services - they have bugs)
        services.AddSingleton<WhisperSpeechTranscriber>();

        services.AddHttpClient("GoogleSpeech");
        services.AddSingleton<GoogleSpeechTranscriber>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("GoogleSpeech");
            var options = sp.GetRequiredService<IOptions<GoogleSpeechToTextOptions>>();
            var logger = sp.GetRequiredService<ILogger<GoogleSpeechTranscriber>>();
            return new GoogleSpeechTranscriber(httpClient, options, logger);
        });

        // Register STT provider factory with the full ISpeechTranscriber collection.
        // Each provider self-declares its ProviderKey/DatabaseName, so adding a new
        // provider is a factory-free change — just register the concrete provider
        // here and add it to this array.
        //
        // NOTE: we cannot use sp.GetServices<ISpeechTranscriber>() here because the
        // downstream AddSingleton<ISpeechTranscriber>(...) (FallbackSpeechTranscriber
        // wrapper) shares the same interface and depends transitively on the factory
        // — auto-discovery would create a circular DI dependency. The factory is
        // still registry-driven (no switch statement); this array is the one place
        // where primary providers are enumerated.
        services.AddSingleton<ISpeechTranscriberFactory>(sp =>
        {
            IEnumerable<ISpeechTranscriber> providers = new ISpeechTranscriber[]
            {
                sp.GetRequiredService<WhisperSpeechTranscriber>(),
                sp.GetRequiredService<GoogleSpeechTranscriber>()
            };
            var queryProcessor = sp.GetRequiredService<IQueryProcessor>();
            var settings = sp.GetRequiredService<IOptions<SpeechProviderSettings>>();
            var logger = sp.GetRequiredService<ILogger<SpeechTranscriberFactory>>();
            return new SpeechTranscriberFactory(providers, queryProcessor, settings, logger);
        });

        // Register FallbackSpeechTranscriber or single provider based on settings.
        // Note: When FallbackSpeechTranscriber is used, LastUsedProviderId tracks which provider
        // was actually used. This is a singleton, so LastUsedProviderId access is thread-safe
        // via Volatile/Interlocked. Alternative approach would be to add ProviderId to
        // TranscriptionResult, but that would require changes to the interface contract.
        services.AddSingleton<ISpeechTranscriber>(sp =>
        {
            var factory = sp.GetRequiredService<ISpeechTranscriberFactory>();
            var settings = sp.GetRequiredService<IOptions<SpeechProviderSettings>>().Value;
            var logger = sp.GetRequiredService<ILogger<FallbackSpeechTranscriber>>();

            var primary = factory.GetProvider(settings.PrimaryProvider);
            var fallback = factory.GetProvider(settings.FallbackProvider);

            // If fallback is disabled or not available, return primary provider directly.
            // In this case, caller should use factory.GetProviderId() for provider tracking.
            if (!settings.EnableFallback || primary == null || fallback == null)
            {
                return primary ?? fallback ?? throw new InvalidOperationException("No STT provider available");
            }

            // Return FallbackSpeechTranscriber with both providers
            return new FallbackSpeechTranscriber(primary, fallback, factory, logger, settings);
        });

        services.AddSingleton<ITranscriptionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TranscriptionService>>();
            var transcriber = sp.GetRequiredService<ISpeechTranscriber>();
            var config = sp.GetRequiredService<IConfiguration>();
            var textFilter = sp.GetRequiredService<ITextFilter>();
            var lightweightTextFilter = sp.GetRequiredService<ILightweightTextFilter>();
            var llmProviderFactory = sp.GetRequiredService<ILlmProviderFactory>();
            var racingLlmProvider = sp.GetRequiredService<IRacingLlmProvider>();
            var llmProviderOptions = sp.GetRequiredService<IOptions<LlmProviderOptions>>();
            return new TranscriptionService(logger, transcriber, config, textFilter, lightweightTextFilter, llmProviderFactory, racingLlmProvider, llmProviderOptions);
        });

        // Read from "OpenCode:Url" (nested key matching appsettings.json). No
        // fallback — a missing value surfaces as a clear failure in the
        // OpenCodeClient constructor rather than silently connecting to
        // a stale localhost port.
        var openCodeUrl = configuration["OpenCode:Url"] ?? string.Empty;
        services.AddSingleton(_ => new OpenCodeClient(openCodeUrl));
        services.AddSingleton<ITextInputService, TextInputService>();
    }

    private static void AddKeyboardServices(this IServiceCollection services)
    {
        services.AddSingleton<IClipboardManager, WlClipboardManager>();
        services.AddSingleton<ITerminalDetector, WaylandTerminalDetector>();
        services.AddSingleton<IGdbusWindowDetector, GdbusWindowDetector>();
        services.AddSingleton<ITmuxCliAppMatcher, TmuxCliAppMatcher>();
        services.AddSingleton<CliAppDetectionCache>();
        services.AddSingleton<ICliAppDetector, TerminalCliAppDetector>();
        services.AddSingleton<IDotoolProcessRunner, DotoolProcessRunner>();
        services.AddSingleton<IClipboardPasteOrchestrator, ClipboardPasteOrchestrator>();
        services.AddSingleton<IKeyboardSimulationService, XDoToolKeyboardService>();

        services.AddSingleton<IKeyboardLedReader, LinuxKeyboardLedReader>();
        services.AddSingleton<IKeyboardDeviceDiscovery, LinuxKeyboardDeviceDiscovery>();

        services.AddSingleton<IKeyboardMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EvdevKeyboardMonitor>>();
            var ledReader = sp.GetRequiredService<IKeyboardLedReader>();
            var deviceDiscovery = sp.GetRequiredService<IKeyboardDeviceDiscovery>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new EvdevKeyboardMonitor(logger, ledReader, deviceDiscovery, options.Value.KeyboardDevice);
        });
    }

    private static void AddSoundEffectPlayers(this IServiceCollection services)
    {
        services.AddKeyedSingleton<ISoundEffectPlayer, TypingSoundPlayer>("typing", (sp, _) =>
        {
            var logger = sp.GetRequiredService<ILogger<TypingSoundPlayer>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var soundsPath = Path.Combine(AppContext.BaseDirectory, "..", "sounds");
            var audioSink = configuration["NotificationAudio:AudioSink"];
            return TypingSoundPlayer.CreateFromDirectory(logger, soundsPath, "write.mp3", audioSink);
        });

        services.AddKeyedSingleton<ISoundEffectPlayer, CancelSoundPlayer>("cancel", (sp, _) =>
        {
            var logger = sp.GetRequiredService<ILogger<CancelSoundPlayer>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var soundsPath = Path.Combine(AppContext.BaseDirectory, "..", "sounds");
            var audioSink = configuration["NotificationAudio:AudioSink"];
            return CancelSoundPlayer.CreateFromDirectory(logger, soundsPath, "paper-rip.mp3", audioSink);
        });

        services.AddKeyedSingleton<ISoundEffectPlayer, RecordingStartSoundPlayer>("recording-start", (sp, _) =>
        {
            var logger = sp.GetRequiredService<ILogger<RecordingStartSoundPlayer>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var soundsPath = Path.Combine(AppContext.BaseDirectory, "..", "sounds");
            var audioSink = configuration["NotificationAudio:AudioSink"];
            return RecordingStartSoundPlayer.CreateFromDirectory(logger, soundsPath, "recording-start.mp3", audioSink);
        });
    }

    private static void AddStateMachineServices(this IServiceCollection services)
    {
        services.AddSingleton<IManualMuteService, ManualMuteService>();

        services.AddSingleton<IVoiceStateMachine>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VoiceStateMachine>>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new VoiceStateMachine(logger, options.Value.StartMuted);
        });

        services.AddSingleton<Voice.StateMachine.IDictationStateMachine, Voice.StateMachine.DictationStateMachine>();

        services.AddSingleton<ISpeechBufferManager, SpeechBufferManager>();
        services.AddSingleton<ICommandDetectionService, CommandDetectionService>();
        services.AddSingleton<IExternalServiceClient, ExternalServiceClient>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
    }

    private static void AddVoiceLlmServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPromptCache>(sp =>
        {
            var loader = sp.GetRequiredService<IPromptLoader>();
            var logger = sp.GetRequiredService<ILogger<ReloadablePromptCache>>();
            return new ReloadablePromptCache(loader, logger);
        });

        // Register HTTP clients for LLM providers
        services.AddHttpClient("Mistral");
        services.AddHttpClient("Zen");
        services.AddHttpClient("Mercury");

        // SystemPromptResolver encapsulates the CLI-app / window-pattern / default
        // prompt priority cascade that every provider needs. Registered once so all
        // three providers share the same logic + caching semantics.
        services.AddSingleton<ISystemPromptResolver, SystemPromptResolver>();

        // Register MistralProvider
        services.AddSingleton<ILlmProvider, MistralProvider>(sp =>
            new MistralProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Mistral"),
                sp.GetRequiredService<IOptions<MistralOptions>>(),
                sp.GetRequiredService<IPromptCache>(),
                sp.GetRequiredService<ILogger<MistralProvider>>(),
                sp.GetRequiredService<IQueryProcessor>(),
                sp.GetRequiredService<ISystemPromptResolver>(),
                sp.GetRequiredService<IServiceScopeFactory>()));

        // Register ZenProvider
        services.AddSingleton<ILlmProvider, ZenProvider>(sp =>
            new ZenProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Zen"),
                sp.GetRequiredService<IOptions<ZenOptions>>(),
                sp.GetRequiredService<IPromptCache>(),
                sp.GetRequiredService<ILogger<ZenProvider>>(),
                sp.GetRequiredService<IQueryProcessor>(),
                sp.GetRequiredService<ISystemPromptResolver>(),
                sp.GetRequiredService<IServiceScopeFactory>()));

        // Register MercuryProvider
        services.AddSingleton<ILlmProvider, MercuryProvider>(sp =>
            new MercuryProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Mercury"),
                sp.GetRequiredService<IOptions<MercuryOptions>>(),
                sp.GetRequiredService<IPromptCache>(),
                sp.GetRequiredService<ILogger<MercuryProvider>>(),
                sp.GetRequiredService<IQueryProcessor>(),
                sp.GetRequiredService<ISystemPromptResolver>(),
                sp.GetRequiredService<IServiceScopeFactory>()));

        // Register LlmProviderFactory
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

        // Register PromptResolver (uses IServiceScopeFactory for thread-safe DbContext access)
        services.AddSingleton<IPromptResolver, PromptResolver>();

        // Register RacingLlmProvider
        services.AddSingleton<IRacingLlmProvider, RacingLlmProvider>();
    }

    private static void AddTextFilterServices(this IServiceCollection services)
    {
        var textFiltersConfigPath = Path.Combine(AppContext.BaseDirectory, "Filters", "text-filters.json");

        services.AddSingleton<ITranscriptionCorrectionRepository, TranscriptionCorrectionRepository>();

        // Register DatabaseCorrectionFilterStrategy as both a concrete singleton (for the
        // lightweight filter — Quick Dictation needs DB corrections like "cloud kód" →
        // "Claude Code" without going through the full LLM pipeline) and as an
        // ITextFilterStrategy (so it also runs in the full CompositeTextFilter for normal
        // dictation). The same instance is shared by both registrations so the in-memory
        // correction cache is used by both code paths.
        //
        // Use GetRequiredService for the repository — Quick Dictation correctness now
        // depends on it. A missing registration would silently disable corrections,
        // which is a regression we want to fail fast on.
        services.AddSingleton<DatabaseCorrectionFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DatabaseCorrectionFilterStrategy>>();
            var repository = sp.GetRequiredService<ITranscriptionCorrectionRepository>();
            return new DatabaseCorrectionFilterStrategy(logger, repository);
        });
        services.AddSingleton<ITextFilterStrategy>(sp =>
            sp.GetRequiredService<DatabaseCorrectionFilterStrategy>());

        // Pre-load + periodically refresh the corrections cache off the request
        // path. Without this, the very first transcription after startup (or
        // every 5 minutes of idle) would block the audio pipeline on a
        // synchronous EF Core call. The hosted service refreshes every 4
        // minutes — under the strategy's 5-minute TTL — so the hot path
        // always finds a fresh cache.
        services.AddHostedService<DatabaseCorrectionCacheWarmupService>();

        services.AddSingleton<ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<FileReplacementFilterStrategy>>();
            return new FileReplacementFilterStrategy(logger, textFiltersConfigPath);
        });

        services.AddSingleton<ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RemovePatternsFilterStrategy>>();
            return new RemovePatternsFilterStrategy(logger, textFiltersConfigPath);
        });

        // Register WhisperHallucinationFilterStrategy as both a concrete singleton (for the
        // lightweight filter) and as an ITextFilterStrategy (so it also runs in the full
        // CompositeTextFilter for normal dictation).
        services.AddSingleton<WhisperHallucinationFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WhisperHallucinationFilterStrategy>>();
            return new WhisperHallucinationFilterStrategy(logger, textFiltersConfigPath);
        });
        services.AddSingleton<ITextFilterStrategy>(sp =>
            sp.GetRequiredService<WhisperHallucinationFilterStrategy>());

        // Register WhitespaceFilterStrategy as both concrete (for lightweight filter) and as
        // ITextFilterStrategy (for full composite). Single instance shared by both.
        services.AddSingleton<WhitespaceFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WhitespaceFilterStrategy>>();
            return new WhitespaceFilterStrategy(logger);
        });
        services.AddSingleton<ITextFilterStrategy>(sp =>
            sp.GetRequiredService<WhitespaceFilterStrategy>());

        services.AddSingleton<ITextFilter, CompositeTextFilter>();
        services.AddSingleton<ILightweightTextFilter, LightweightTextFilter>();
    }
}
