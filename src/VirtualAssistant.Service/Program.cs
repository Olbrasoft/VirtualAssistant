using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using OpenCode.DotnetClient;
using VirtualAssistant.Data.EntityFrameworkCore;

namespace Olbrasoft.VirtualAssistant.Service;

/// <summary>
/// Request model for assistant speech start endpoint.
/// </summary>
public record AssistantSpeechStartRequest(string Text);

/// <summary>
/// Request model for TTS notify endpoint (from OpenCode plugin).
/// </summary>
public record TtsNotifyRequest(string Text);

/// <summary>
/// Request model for mute control endpoint (from PushToTalk service).
/// </summary>
public record MuteRequest(bool Muted);

public class Program
{
    private static WebApplication? _app;
    private static TrayIconService? _trayService;
    private static CancellationTokenSource? _cts;
    private static FileStream? _lockFile;
    private const string LockFilePath = "/tmp/virtual-assistant.lock";

    [STAThread]
    public static void Main(string[] args)
    {
        // Single instance check - try to acquire exclusive lock
        if (!TryAcquireSingleInstanceLock())
        {
            Console.WriteLine("ERROR: VirtualAssistant is already running!");
            Console.WriteLine("Only one instance is allowed.");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                   VirtualAssistant Service                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        _cts = new CancellationTokenSource();

        // Build WebApplication with all services
        var builder = WebApplication.CreateBuilder(args);

        // Configure Kestrel to listen on specific port
        var listenerPort = builder.Configuration.GetValue<int>("ListenerApiPort", 5055);
        builder.WebHost.UseUrls($"http://localhost:{listenerPort}");

        // Configuration
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        ConfigureServices(builder);

        _app = builder.Build();

        // Configure API endpoints
        ConfigureEndpoints(_app);

        // Get services
        var muteService = _app.Services.GetRequiredService<IManualMuteService>();
        var options = _app.Services.GetRequiredService<IOptions<ContinuousListenerOptions>>();
        
        // Create tray icon service (not from DI - needs special lifecycle)
        _trayService = new TrayIconService(muteService, options.Value.LogViewerPort);

        try
        {
            // Initialize tray (must be on main thread)
            _trayService.Initialize(OnQuitRequested);
            Console.WriteLine("Tray icon initialized");
            Console.WriteLine($"API listening on http://localhost:{listenerPort}");

            // Start WebApplication in background
            var hostTask = Task.Run(async () =>
            {
                try
                {
                    await _app.RunAsync(_cts.Token);
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
                OnQuitRequested();
                _trayService.QuitMainLoop();
            };

            Console.WriteLine("VirtualAssistant running - tray icon active");
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
            ReleaseSingleInstanceLock();
        }

        Console.WriteLine("VirtualAssistant stopped");
    }

    /// <summary>
    /// Tries to acquire an exclusive lock to ensure only one instance runs.
    /// </summary>
    private static bool TryAcquireSingleInstanceLock()
    {
        try
        {
            _lockFile = new FileStream(
                LockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            
            // Write PID to lock file for debugging
            var pid = Environment.ProcessId.ToString();
            _lockFile.SetLength(0);
            var bytes = System.Text.Encoding.UTF8.GetBytes(pid);
            _lockFile.Write(bytes, 0, bytes.Length);
            _lockFile.Flush();
            
            return true;
        }
        catch (IOException)
        {
            // Lock file is held by another process
            return false;
        }
    }

    /// <summary>
    /// Releases the single instance lock.
    /// </summary>
    private static void ReleaseSingleInstanceLock()
    {
        try
        {
            _lockFile?.Dispose();
            _lockFile = null;
            
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static void OnQuitRequested()
    {
        Console.WriteLine("Quit requested - stopping services...");
        _cts?.Cancel();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Register ContinuousListener options
        builder.Services.Configure<ContinuousListenerOptions>(
            builder.Configuration.GetSection(ContinuousListenerOptions.SectionName));

        // Register VirtualAssistant Data services (DbContext + CQRS handlers)
        var connectionString = builder.Configuration.GetConnectionString("VirtualAssistantDb")
            ?? throw new InvalidOperationException("Connection string 'VirtualAssistantDb' not found.");
        builder.Services.AddVirtualAssistantData(connectionString);

        // String similarity for echo cancellation
        builder.Services.AddSingleton<IStringSimilarity, LevenshteinSimilarity>();

        // Assistant speech tracker for echo cancellation
        builder.Services.AddSingleton<AssistantSpeechTrackerService>();

        // Singleton services
        builder.Services.AddSingleton<SileroVadOnnxModel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new SileroVadOnnxModel(options.Value.SileroVadModelPath);
        });

        builder.Services.AddSingleton<AudioCaptureService>();
        builder.Services.AddSingleton<VadService>();
        builder.Services.AddSingleton<WhisperNetTranscriber>();
        builder.Services.AddSingleton<TranscriptionService>();

        // Prompt loader for LLM routers
        builder.Services.AddSingleton<IPromptLoader, PromptLoader>();

        // LLM Routers - register as BaseLlmRouterService for MultiProvider to collect
        // HttpClient is transient by default
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<BaseLlmRouterService, CerebrasRouterService>();
        builder.Services.AddSingleton<BaseLlmRouterService, GroqRouterService>();
        builder.Services.AddSingleton<BaseLlmRouterService, MistralRouterService>();
        builder.Services.AddSingleton<ILlmRouterService, MultiProviderLlmRouter>();

        // Text input service for OpenCode
        var openCodeUrl = builder.Configuration["OpenCodeUrl"] ?? "http://localhost:4096";
        builder.Services.AddSingleton(new OpenCodeClient(openCodeUrl));
        builder.Services.AddSingleton<TextInputService>();

        // Mute service (shared between tray, keyboard monitor, and continuous listener)
        builder.Services.AddSingleton<IManualMuteService, ManualMuteService>();

        // Keyboard monitor
        builder.Services.AddSingleton<IKeyboardMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EvdevKeyboardMonitor>>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            return new EvdevKeyboardMonitor(logger, options.Value.KeyboardDevice);
        });

        // TTS Service for text-to-speech
        builder.Services.AddSingleton<TtsService>();

        // Background workers
        builder.Services.AddHostedService<KeyboardMonitorWorker>();
        builder.Services.AddHostedService<ContinuousListenerWorker>();
    }

    private static void ConfigureEndpoints(WebApplication app)
    {
        var speechTracker = app.Services.GetRequiredService<AssistantSpeechTrackerService>();

        // Called by TTS MCP server when it starts speaking
        app.MapPost("/api/assistant-speech/start", (AssistantSpeechStartRequest request) =>
        {
            // Green output for TTS text being spoken
            Console.WriteLine($"\u001b[92;1m🗣️ TTS: \"{request.Text}\"\u001b[0m");
            speechTracker.StartSpeaking(request.Text);
            return Results.Ok(new { status = "started", text = request.Text });
        });

        // Called by TTS MCP server when it stops speaking
        app.MapPost("/api/assistant-speech/end", () =>
        {
            speechTracker.StopSpeaking();
            return Results.Ok(new { status = "ended" });
        });

        // Status endpoint for debugging
        app.MapGet("/api/assistant-speech/status", () =>
        {
            return Results.Ok(new 
            { 
                historyCount = speechTracker.GetHistoryCount()
            });
        });

        // Health check
        app.MapGet("/health", () => Results.Ok("OK"));

        // TTS Notify endpoint - receives text from OpenCode plugin and speaks it
        app.MapPost("/api/tts/notify", async (TtsNotifyRequest request, TtsService ttsService, ILogger<Program> logger) =>
        {
            // Cyan output for received notification
            Console.WriteLine($"\u001b[96;1m📩 TTS Notify: \"{request.Text}\"\u001b[0m");
            logger.LogInformation("TTS Notify received: {Text}", request.Text);
            
            // Speak the text asynchronously (fire and forget for quick response)
            _ = Task.Run(async () =>
            {
                try
                {
                    await ttsService.SpeakAsync(request.Text);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error speaking text: {Text}", request.Text);
                }
            });
            
            return Results.Ok(new { success = true, message = "Notification received", text = request.Text });
        });

        // TTS Queue status endpoint - returns current queue count
        app.MapGet("/api/tts/queue", (TtsService ttsService) =>
        {
            return Results.Ok(new { queueCount = ttsService.QueueCount });
        });

        // TTS Flush queue endpoint - plays all queued messages (called by PushToTalk after dictation ends)
        app.MapPost("/api/tts/flush-queue", async (TtsService ttsService, ILogger<Program> logger) =>
        {
            var queueCount = ttsService.QueueCount;
            if (queueCount > 0)
            {
                Console.WriteLine($"\u001b[93;1m🔊 TTS Flush Queue: {queueCount} message(s) pending\u001b[0m");
            }
            logger.LogInformation("TTS Flush Queue requested. Queue count: {Count}", queueCount);
            
            // Flush the queue asynchronously (fire and forget for quick response)
            _ = Task.Run(async () =>
            {
                try
                {
                    await ttsService.FlushQueueAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error flushing TTS queue");
                }
            });
            
            return Results.Ok(new { success = true, message = "Flush queue initiated", queueCount });
        });

        // Mute control endpoint - allows PushToTalk to control mute state (changes icon)
        var muteService = app.Services.GetRequiredService<IManualMuteService>();
        app.MapPost("/api/mute", (MuteRequest request, ILogger<Program> logger) =>
        {
            var previousState = muteService.IsMuted;
            muteService.SetMuted(request.Muted);
            
            var action = request.Muted ? "🔇 MUTED" : "🔊 UNMUTED";
            Console.WriteLine($"\u001b[95;1m{action} (via API)\u001b[0m");
            logger.LogInformation("Mute state changed via API: {PreviousState} -> {NewState}", previousState, request.Muted);
            
            return Results.Ok(new { success = true, muted = muteService.IsMuted, previousState });
        });

        // Get current mute state
        app.MapGet("/api/mute", () =>
        {
            return Results.Ok(new { muted = muteService.IsMuted });
        });
    }
}
