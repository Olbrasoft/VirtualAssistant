using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using OpenCode.DotnetClient;
using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub;
using VirtualAssistant.GitHub.Services;
using VirtualAssistant.Core;
using VirtualAssistant.Core.Services;
using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Service;

/// <summary>
/// Request model for assistant speech start endpoint.
/// </summary>
public record AssistantSpeechStartRequest(string Text);

/// <summary>
/// Request model for TTS notify endpoint (from OpenCode plugin).
/// Source identifies the AI client for voice differentiation.
/// </summary>
public class TtsNotifyRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Source { get; set; }
}

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

        // Apply pending database migrations automatically
        using (var scope = _app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
                if (pendingMigrations.Count > 0)
                {
                    logger.LogInformation("Applying {Count} pending database migrations: {Migrations}",
                        pendingMigrations.Count, string.Join(", ", pendingMigrations));
                    Console.WriteLine($"📦 Applying {pendingMigrations.Count} pending migration(s)...");
                }

                dbContext.Database.Migrate();

                if (pendingMigrations.Count > 0)
                {
                    logger.LogInformation("Database migrations applied successfully");
                    Console.WriteLine("✅ Database migrations applied successfully");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply database migrations");
                Console.WriteLine($"❌ Database migration failed: {ex.Message}");
                throw;
            }
        }

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

        // Register GitHub sync services
        builder.Services.AddGitHubServices(builder.Configuration);

        // Register Core services (AgentHubService)
        builder.Services.AddCoreServices();

        // Claude dispatch service for headless mode execution
        builder.Services.Configure<ClaudeDispatchOptions>(
            builder.Configuration.GetSection(ClaudeDispatchOptions.SectionName));
        builder.Services.AddSingleton<IClaudeDispatchService, ClaudeDispatchService>();

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

        // Repeat text intent detection service (for PTT history feature)
        builder.Services.AddSingleton<IRepeatTextIntentService, RepeatTextIntentService>();

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

        // TTS Provider Chain Configuration
        builder.Services.Configure<TtsProviderChainOptions>(
            builder.Configuration.GetSection(TtsProviderChainOptions.SectionName));
        builder.Services.Configure<AzureTtsOptions>(
            builder.Configuration.GetSection(AzureTtsOptions.SectionName));
        builder.Services.Configure<HttpEdgeTtsOptions>(
            builder.Configuration.GetSection(HttpEdgeTtsOptions.SectionName));
        builder.Services.Configure<VoiceRssOptions>(
            builder.Configuration.GetSection(VoiceRssOptions.SectionName));
        builder.Services.Configure<GoogleTtsOptions>(
            builder.Configuration.GetSection(GoogleTtsOptions.SectionName));
        builder.Services.Configure<Olbrasoft.VirtualAssistant.Voice.Configuration.PiperTtsOptions>(
            builder.Configuration.GetSection(Olbrasoft.VirtualAssistant.Voice.Configuration.PiperTtsOptions.SectionName));

        // Register all TTS providers (order doesn't matter - TtsProviderChain uses config order)
        builder.Services.AddSingleton<ITtsProvider, AzureTtsProvider>();
        builder.Services.AddSingleton<ITtsProvider, HttpEdgeTtsProvider>();
        builder.Services.AddSingleton<ITtsProvider, VoiceRssProvider>();
        builder.Services.AddSingleton<ITtsProvider, GoogleTtsProvider>();
        builder.Services.AddSingleton<ITtsProvider, PiperTtsProvider>();

        // TTS Provider Chain with circuit breaker
        builder.Services.AddSingleton<ITtsProviderChain, TtsProviderChain>();
        builder.Services.AddSingleton<TtsService>();

        // Workspace detection for smart TTS notifications
        builder.Services.AddSingleton<IWorkspaceDetectionService, WorkspaceDetectionService>();

        // VirtualAssistantSpeaker - single entry point for all TTS operations
        builder.Services.AddSingleton<IVirtualAssistantSpeaker, VirtualAssistantSpeaker>();

        // Notification humanization and batching services
        builder.Services.AddSingleton<IHumanizationService, HumanizationService>();
        builder.Services.AddSingleton<INotificationBatchingService, NotificationBatchingService>();

        // Background workers
        builder.Services.AddHostedService<KeyboardMonitorWorker>();
        builder.Services.AddHostedService<ContinuousListenerWorker>();

        // Startup notification (Phase 1: simple "Systém nastartován")
        builder.Services.AddHostedService<StartupNotificationService>();

        // Orphaned task detection (detects stuck tasks after restart, notifies human)
        builder.Services.AddScoped<IOrphanedTaskService, OrphanedTaskService>();
        builder.Services.AddHostedService<OrphanedTaskDetectionService>();

        // Add controllers for API endpoints
        builder.Services.AddControllers();
    }

    private static void ConfigureEndpoints(WebApplication app)
    {
        // Map controller endpoints
        app.MapControllers();

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

        // TTS Notify/Speak endpoint - receives text from OpenCode plugin and speaks it
        // Uses TtsProviderChain with circuit breaker: EdgeTTS → VoiceRSS → gTTS → Piper
        // Both /api/tts/notify and /api/tts/speak are supported (speak is alias for notify)
        var ttsProviderChain = app.Services.GetRequiredService<ITtsProviderChain>();
        var voiceProfilesOptions = app.Services.GetRequiredService<IOptions<TtsVoiceProfilesOptions>>().Value;

        // Helper: Check if CapsLock LED is ON (user is recording)
        static bool IsCapsLockOn()
        {
            try
            {
                var ledPaths = new[]
                {
                    "/sys/class/leds/input0::capslock/brightness",
                    "/sys/class/leds/input1::capslock/brightness",
                    "/sys/class/leds/input2::capslock/brightness",
                    "/sys/class/leds/input3::capslock/brightness"
                };

                foreach (var path in ledPaths)
                {
                    if (File.Exists(path))
                    {
                        var value = File.ReadAllText(path).Trim();
                        if (value == "1") return true;
                    }
                }

                // Dynamic discovery
                if (Directory.Exists("/sys/class/leds"))
                {
                    foreach (var dir in Directory.GetDirectories("/sys/class/leds"))
                    {
                        if (dir.Contains("capslock", StringComparison.OrdinalIgnoreCase))
                        {
                            var brightnessFile = Path.Combine(dir, "brightness");
                            if (File.Exists(brightnessFile))
                            {
                                var value = File.ReadAllText(brightnessFile).Trim();
                                if (value == "1") return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false; // Fail open - allow speech if check fails
            }
        }

        async Task<IResult> HandleTtsSpeak(TtsNotifyRequest request, ILogger<Program> logger)
        {
            var sourceInfo = string.IsNullOrEmpty(request.Source) ? "" : $" [{request.Source}]";
            Console.WriteLine($"\u001b[96;1m📩 TTS Speak{sourceInfo}: \"{request.Text}\"\u001b[0m");
            logger.LogInformation("TTS Speak received from {Source}: {Text}", request.Source ?? "default", request.Text);

            var text = request.Text;
            var source = request.Source ?? "default";

            // Process TTS asynchronously (fire and forget for quick API response)
            _ = Task.Run(async () =>
            {
                try
                {
                    // Check CapsLock before starting - if user is recording, skip
                    if (IsCapsLockOn())
                    {
                        Console.WriteLine("\u001b[93m⚠ TTS skipped (CapsLock ON - recording)\u001b[0m");
                        return;
                    }

                    // Get voice config for this source
                    if (!voiceProfilesOptions.Profiles.TryGetValue(source, out var voiceConfig))
                    {
                        voiceProfilesOptions.Profiles.TryGetValue("default", out voiceConfig);
                        voiceConfig ??= new VoiceConfig("cs-CZ-AntoninNeural", "+20%", "+0%", "+0Hz");
                    }

                    // Use provider chain with circuit breaker
                    var (audio, providerUsed) = await ttsProviderChain.SynthesizeAsync(text, voiceConfig, source);

                    if (audio == null || audio.Length == 0)
                    {
                        Console.WriteLine("\u001b[91m✗ All TTS providers failed\u001b[0m");
                        return;
                    }

                    // EdgeTTS-HTTP plays audio server-side (returns empty array)
                    if (providerUsed == "EdgeTTS-HTTP" && audio.Length == 0)
                    {
                        Console.WriteLine($"\u001b[92m✓ {providerUsed}\u001b[0m");
                        return;
                    }

                    // Check CapsLock again before playing
                    if (IsCapsLockOn())
                    {
                        Console.WriteLine($"\u001b[93m⚠ {providerUsed} generated, but CapsLock ON - not playing\u001b[0m");
                        return;
                    }

                    // Save audio to temp file and play
                    var isWav = providerUsed == "PiperTTS";
                    var tempFile = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid():N}.{(isWav ? "wav" : "mp3")}");
                    try
                    {
                        await File.WriteAllBytesAsync(tempFile, audio);

                        // Use aplay for WAV, mpv/ffplay for MP3
                        var playerProcess = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = isWav ? "aplay" : "ffplay",
                                Arguments = isWav ? tempFile : $"-nodisp -autoexit \"{tempFile}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        playerProcess.Start();

                        // Poll during playback - stop if CapsLock pressed
                        while (!playerProcess.HasExited)
                        {
                            if (IsCapsLockOn())
                            {
                                Console.WriteLine($"\u001b[91m🛑 CapsLock pressed - stopping {providerUsed} playback\u001b[0m");
                                try { playerProcess.Kill(entireProcessTree: true); } catch { }
                                break;
                            }
                            await Task.Delay(100); // Poll every 100ms
                        }

                        if (!playerProcess.HasExited)
                        {
                            await playerProcess.WaitForExitAsync();
                        }
                        Console.WriteLine($"\u001b[92m✓ {providerUsed}\u001b[0m");
                    }
                    finally
                    {
                        try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TTS processing failed");
                }
            });

            var actualSource = request.Source ?? "NOT_SET";
            return Results.Ok(new { success = true, message = "TTS processing", text = request.Text, source = actualSource });
        }

        app.MapPost("/api/tts/notify", HandleTtsSpeak);
        app.MapPost("/api/tts/speak", HandleTtsSpeak);

        // TTS Provider status endpoint - shows circuit breaker status
        app.MapGet("/api/tts/providers", () =>
        {
            var statuses = ttsProviderChain.GetProvidersStatus();
            return Results.Ok(new { providers = statuses });
        });

        // TTS Circuit breaker reset endpoint
        app.MapPost("/api/tts/reset-circuit-breaker", (string? provider) =>
        {
            ttsProviderChain.ResetCircuitBreaker(provider);
            return Results.Ok(new { success = true, message = provider != null ? $"Reset circuit breaker for {provider}" : "Reset all circuit breakers" });
        });

        // TTS Queue status endpoint - returns current queue count
        app.MapGet("/api/tts/queue", (TtsService ttsService) =>
        {
            return Results.Ok(new { queueCount = ttsService.QueueCount });
        });

        // TTS Stop endpoint - stops any currently playing audio (called by PushToTalk when recording starts)
        app.MapPost("/api/tts/stop", (TtsService ttsService, ILogger<Program> logger) =>
        {
            Console.WriteLine($"\u001b[91;1m🛑 TTS Stop requested\u001b[0m");
            logger.LogInformation("TTS Stop requested - stopping playback");
            ttsService.StopPlayback();
            return Results.Ok(new { success = true, message = "Playback stopped" });
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

        // GitHub Sync endpoints
        // POST /api/github/sync/{owner}/{repo} - sync single repository
        app.MapPost("/api/github/sync/{owner}/{repo}", async (
            string owner,
            string repo,
            IGitHubSyncService syncService,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("GitHub sync requested for {Owner}/{Repo}", owner, repo);

            try
            {
                var (repoSynced, issuesSynced) = await syncService.SyncRepositoryAsync(owner, repo);

                if (!repoSynced)
                {
                    logger.LogWarning("Repository {Owner}/{Repo} not found", owner, repo);
                    return Results.NotFound(new { error = $"Repository {owner}/{repo} not found" });
                }

                logger.LogInformation("Synced repository {Owner}/{Repo}: {IssueCount} issues", owner, repo, issuesSynced);
                return Results.Ok(new
                {
                    success = true,
                    repository = $"{owner}/{repo}",
                    issuesSynced
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error syncing {Owner}/{Repo}", owner, repo);
                return Results.Problem($"Sync failed: {ex.Message}");
            }
        });

        // POST /api/github/sync/{owner} - sync all repositories for owner
        app.MapPost("/api/github/sync/{owner}", async (
            string owner,
            IGitHubSyncService syncService,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("GitHub sync requested for all repos of {Owner}", owner);

            try
            {
                var (reposSynced, issuesSynced) = await syncService.SyncAllAsync(owner);

                logger.LogInformation("Synced all repositories for {Owner}: {RepoCount} repos, {IssueCount} issues",
                    owner, reposSynced, issuesSynced);
                return Results.Ok(new
                {
                    success = true,
                    owner,
                    repositoriesSynced = reposSynced,
                    issuesSynced
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error syncing all repos for {Owner}", owner);
                return Results.Problem($"Sync failed: {ex.Message}");
            }
        });

        // GitHub Sync status/health endpoint
        app.MapGet("/api/github/sync/status", (GitHubSyncBackgroundService syncBackgroundService) =>
        {
            var status = syncBackgroundService.GetStatus();
            return Results.Ok(status);
        });

        // POST /api/github/embeddings - generate embeddings for issues without them
        app.MapPost("/api/github/embeddings", async (
            IGitHubSyncService syncService,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("Embedding generation requested");

            try
            {
                var count = await syncService.GenerateMissingEmbeddingsAsync();
                logger.LogInformation("Generated embeddings for {Count} issues", count);
                return Results.Ok(new
                {
                    success = true,
                    embeddingsGenerated = count
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating embeddings");
                return Results.Problem($"Embedding generation failed: {ex.Message}");
            }
        });

        // GET /api/github/search - semantic search for similar issues
        app.MapGet("/api/github/search", async (
            string q,
            string? target,
            int? limit,
            IGitHubSearchService searchService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            var searchTarget = target?.ToLower() switch
            {
                "title" => SearchTarget.Title,
                "body" => SearchTarget.Body,
                _ => SearchTarget.Both
            };

            logger.LogInformation("GitHub search: q={Query}, target={Target}, limit={Limit}",
                q, searchTarget, limit ?? 10);

            try
            {
                var results = await searchService.SearchSimilarAsync(q, searchTarget, limit ?? 10, ct);
                return Results.Ok(new
                {
                    query = q,
                    target = searchTarget.ToString().ToLower(),
                    count = results.Count,
                    results
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching issues");
                return Results.Problem($"Search failed: {ex.Message}");
            }
        });

        // GET /api/github/duplicates - find potential duplicate issues
        app.MapGet("/api/github/duplicates", async (
            string title,
            string? body,
            float? threshold,
            IGitHubSearchService searchService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return Results.BadRequest(new { error = "Query parameter 'title' is required" });
            }

            var effectiveThreshold = threshold ?? 0.8f;
            if (effectiveThreshold < 0 || effectiveThreshold > 1)
            {
                return Results.BadRequest(new { error = "Threshold must be between 0 and 1" });
            }

            logger.LogInformation("GitHub duplicate check: title={Title}, threshold={Threshold}",
                title, effectiveThreshold);

            try
            {
                var results = await searchService.FindDuplicatesAsync(title, body, effectiveThreshold, ct);
                return Results.Ok(new
                {
                    title,
                    threshold = effectiveThreshold,
                    count = results.Count,
                    duplicates = results
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error finding duplicates");
                return Results.Problem($"Duplicate check failed: {ex.Message}");
            }
        });

        // GET /api/github/issues/open/{owner}/{repo} - get open issues for a repository
        app.MapGet("/api/github/issues/open/{owner}/{repo}", async (
            string owner,
            string repo,
            IGitHubSearchService searchService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var repoFullName = $"{owner}/{repo}";
            logger.LogInformation("Getting open issues for {Repo}", repoFullName);

            try
            {
                var results = await searchService.GetOpenIssuesAsync(repoFullName, ct);
                return Results.Ok(new
                {
                    repository = repoFullName,
                    count = results.Count,
                    issues = results
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting open issues for {Repo}", repoFullName);
                return Results.Problem($"Failed to get open issues: {ex.Message}");
            }
        });
    }
}
