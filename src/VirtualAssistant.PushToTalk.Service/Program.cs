using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.PushToTalk;
using Olbrasoft.VirtualAssistant.PushToTalk.Service;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Hubs;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;
using Olbrasoft.VirtualAssistant.PushToTalk.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Services;

// Disambiguate types that exist in multiple namespaces
using PttManualMuteService = Olbrasoft.VirtualAssistant.PushToTalk.Service.Services.ManualMuteService;
using PttEvdevKeyboardMonitor = Olbrasoft.VirtualAssistant.PushToTalk.EvdevKeyboardMonitor;

var builder = WebApplication.CreateBuilder(args);

// Get configuration values
var keyboardDevice = builder.Configuration.GetValue<string?>("PushToTalkDictation:KeyboardDevice");
var ggmlModelPath = builder.Configuration.GetValue<string>("PushToTalkDictation:GgmlModelPath") 
    ?? Path.Combine(AppContext.BaseDirectory, "models", "ggml-medium.bin");
var whisperLanguage = builder.Configuration.GetValue<string>("PushToTalkDictation:WhisperLanguage") ?? "cs";

// SignalR
builder.Services.AddSignalR();

// CORS (for web clients)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// PTT Notifier service
builder.Services.AddSingleton<IPttNotifier, PttNotifier>();

// Manual mute service (ScrollLock) - register as concrete type for injection
// Also register interface for backwards compatibility
builder.Services.AddSingleton<PttManualMuteService>();
builder.Services.AddSingleton<Olbrasoft.VirtualAssistant.Core.Services.IManualMuteService>(sp => sp.GetRequiredService<PttManualMuteService>());

// Register services
builder.Services.AddSingleton<IKeyboardMonitor>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PttEvdevKeyboardMonitor>>();
    return new PttEvdevKeyboardMonitor(logger, keyboardDevice);
});

builder.Services.AddSingleton<IAudioRecorder>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<AlsaAudioRecorder>>();
    return new AlsaAudioRecorder(logger);
});

builder.Services.AddSingleton<ISpeechTranscriber>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WhisperNetTranscriber>>();
    return new WhisperNetTranscriber(logger, ggmlModelPath, whisperLanguage);
});

// Auto-detect display server (X11/Wayland) and use appropriate text typer
builder.Services.AddSingleton<ITextTyper>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var displayServer = TextTyperFactory.GetDisplayServerName();
    logger.LogInformation("Detected display server: {DisplayServer}", displayServer);
    return TextTyperFactory.Create(loggerFactory);
});

// HTTP client for TTS stop functionality
builder.Services.AddHttpClient<DictationWorker>();

// Register worker
builder.Services.AddHostedService<DictationWorker>();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddSystemdConsole();

var app = builder.Build();

app.UseCors();

// Map SignalR hub
app.MapHub<PttHub>("/hubs/ptt");

// Health check endpoint
app.MapGet("/", () => Results.Ok(new { service = "VirtualAssistant.PushToTalk", status = "running" }));

// Run on port 5050
app.Run("http://localhost:5050");
