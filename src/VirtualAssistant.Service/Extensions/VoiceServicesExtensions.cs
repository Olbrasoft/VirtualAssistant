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

        // Register STT provider factory with explicit provider injection (no service locator)
        // Cast to ISpeechTranscriber to match factory constructor signature
        services.AddSingleton<ISpeechTranscriberFactory>(sp =>
        {
            ISpeechTranscriber whisper = sp.GetRequiredService<WhisperSpeechTranscriber>();
            ISpeechTranscriber google = sp.GetRequiredService<GoogleSpeechTranscriber>();
            var queryProcessor = sp.GetRequiredService<IQueryProcessor>();
            var settings = sp.GetRequiredService<IOptions<SpeechProviderSettings>>();
            var logger = sp.GetRequiredService<ILogger<SpeechTranscriberFactory>>();
            return new SpeechTranscriberFactory(whisper, google, queryProcessor, settings, logger);
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

        var openCodeUrl = configuration["OpenCodeUrl"] ?? "http://localhost:4096";
        services.AddSingleton(_ => new OpenCodeClient(openCodeUrl));
        services.AddSingleton<ITextInputService, TextInputService>();
    }

    private static void AddKeyboardServices(this IServiceCollection services)
    {
        services.AddSingleton<IClipboardManager, WlClipboardManager>();
        services.AddSingleton<ITerminalDetector, WaylandTerminalDetector>();
        services.AddSingleton<ICliAppDetector, TerminalCliAppDetector>();
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

        // Register MistralProvider
        services.AddSingleton<ILlmProvider, MistralProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Mistral");
            var options = sp.GetRequiredService<IOptions<MistralOptions>>();
            var promptCache = sp.GetRequiredService<IPromptCache>();
            var logger = sp.GetRequiredService<ILogger<MistralProvider>>();
            var desktopContextService = sp.GetRequiredService<IDesktopContextService>();
            var queryProcessor = sp.GetRequiredService<IQueryProcessor>();
            var cliAppDetector = sp.GetRequiredService<ICliAppDetector>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new MistralProvider(httpClient, options, promptCache, logger, desktopContextService, queryProcessor, cliAppDetector, scopeFactory);
        });

        // Register ZenProvider
        services.AddSingleton<ILlmProvider, ZenProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Zen");
            var options = sp.GetRequiredService<IOptions<ZenOptions>>();
            var promptCache = sp.GetRequiredService<IPromptCache>();
            var logger = sp.GetRequiredService<ILogger<ZenProvider>>();
            var desktopContextService = sp.GetRequiredService<IDesktopContextService>();
            var queryProcessor = sp.GetRequiredService<IQueryProcessor>();
            var cliAppDetector = sp.GetRequiredService<ICliAppDetector>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new ZenProvider(httpClient, options, promptCache, logger, desktopContextService, queryProcessor, cliAppDetector, scopeFactory);
        });

        // Register MercuryProvider
        services.AddSingleton<ILlmProvider, MercuryProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Mercury");
            var options = sp.GetRequiredService<IOptions<MercuryOptions>>();
            var promptCache = sp.GetRequiredService<IPromptCache>();
            var logger = sp.GetRequiredService<ILogger<MercuryProvider>>();
            var desktopContextService = sp.GetRequiredService<IDesktopContextService>();
            var queryProcessor = sp.GetRequiredService<IQueryProcessor>();
            var cliAppDetector = sp.GetRequiredService<ICliAppDetector>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new MercuryProvider(httpClient, options, promptCache, logger, desktopContextService, queryProcessor, cliAppDetector, scopeFactory);
        });

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

        services.AddSingleton<ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DatabaseCorrectionFilterStrategy>>();
            var repository = sp.GetService<ITranscriptionCorrectionRepository>();
            return new DatabaseCorrectionFilterStrategy(logger, repository);
        });

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
