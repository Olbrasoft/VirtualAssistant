using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.PushToTalk;
using Olbrasoft.VirtualAssistant.PushToTalk.Service;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Hubs;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Tray;
using Olbrasoft.VirtualAssistant.PushToTalk.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Services;

// Disambiguate types that exist in multiple namespaces
using PttManualMuteService = Olbrasoft.VirtualAssistant.PushToTalk.Service.Services.ManualMuteService;
using PttEvdevKeyboardMonitor = Olbrasoft.VirtualAssistant.PushToTalk.EvdevKeyboardMonitor;

// Static fields for GTK integration
WebApplication? _app = null;
TranscriptionTrayService? _trayService = null;
CancellationTokenSource? _cts = null;

_cts = new CancellationTokenSource();

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

// Transcription history service (single-level history for repeat functionality)
builder.Services.AddSingleton<ITranscriptionHistory, TranscriptionHistory>();

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

// Typing sound player for transcription feedback
builder.Services.AddSingleton<TypingSoundPlayer>();

// Transcription tray service (not from DI - needs special lifecycle with GTK)
builder.Services.AddSingleton<TranscriptionTrayService>();

// HTTP client for TTS stop functionality
builder.Services.AddHttpClient<DictationWorker>();

// Register worker
builder.Services.AddHostedService<DictationWorker>();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddSystemdConsole();

_app = builder.Build();

_app.UseCors();

// Map SignalR hub
_app.MapHub<PttHub>("/hubs/ptt");

// Health check endpoint
_app.MapGet("/", () => Results.Ok(new { service = "VirtualAssistant.PushToTalk", status = "running" }));

// Get tray service from DI
var pttNotifier = _app.Services.GetRequiredService<IPttNotifier>();
var trayLogger = _app.Services.GetRequiredService<ILogger<TranscriptionTrayService>>();
var typingSoundPlayer = _app.Services.GetRequiredService<TypingSoundPlayer>();
_trayService = new TranscriptionTrayService(trayLogger, pttNotifier, typingSoundPlayer);

try
{
    // Initialize tray (must be on main thread for GTK)
    _trayService.Initialize(() =>
    {
        Console.WriteLine("Quit requested from tray - stopping services...");
        _cts?.Cancel();
    });
    
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║            Push-to-Talk Dictation Service                    ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("Transcription tray icon initialized");
    Console.WriteLine("API listening on http://localhost:5050");

    // Start WebApplication in background
    var hostTask = Task.Run(async () =>
    {
        try
        {
            await _app.RunAsync(_cts!.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    });

    // Handle Ctrl+C
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        Console.WriteLine("\nCtrl+C pressed - shutting down...");
        _cts?.Cancel();
        _trayService?.QuitMainLoop();
    };

    Console.WriteLine("Push-to-Talk running - tray icon active");
    Console.WriteLine("Press Ctrl+C or use tray menu to exit");
    Console.WriteLine();

    // Run GTK main loop (blocks until quit)
    _trayService.RunMainLoop();

    // Wait for host to finish
    hostTask.Wait(TimeSpan.FromSeconds(5));
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}
finally
{
    _trayService?.Dispose();
    _app?.DisposeAsync().AsTask().Wait();
    _cts?.Dispose();
}

Console.WriteLine("Push-to-Talk stopped");
