using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Extensions;
using Olbrasoft.VirtualAssistant.Service.Middleware;
using Olbrasoft.VirtualAssistant.Service.Tray;

namespace Olbrasoft.VirtualAssistant.Service;

/// <summary>
/// VirtualAssistant Service entry point.
/// Manages service lifecycle, tray icon, and single instance locking.
/// </summary>
public class Program
{
    private static WebApplication? _app;
    private static VirtualAssistantTrayService? _trayService;
    private static CancellationTokenSource? _cts;
    private static FileStream? _lockFile;
    private static string _lockFilePath = "/tmp/virtual-assistant.lock"; // Default, overridden from config

    public static async Task Main(string[] args)
    {
        // Load configuration early to get lock file path
        // In production: /opt/olbrasoft/virtual-assistant/app/../config/appsettings.json
        var configPath = Path.Combine(AppContext.BaseDirectory, "../config/appsettings.json");
        var earlyConfig = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true)
            .AddEnvironmentVariables()
            .Build();

        _lockFilePath = earlyConfig["SystemPaths:VirtualAssistantLockFile"]
            ?? "/tmp/virtual-assistant.lock";

        // Single instance check
        if (!TryAcquireSingleInstanceLock())
        {
            Console.WriteLine("ERROR: VirtualAssistant is already running!");
            Console.WriteLine("Only one instance is allowed.");
            Environment.Exit(1);
            return;
        }

        PrintBanner();

        _cts = new CancellationTokenSource();

        // Build WebApplication
        var builder = WebApplication.CreateBuilder(args);

        // Configure Kestrel
        var listenerPort = builder.Configuration.GetValue<int>("ListenerApiPort", 5055);
        builder.WebHost.UseUrls($"http://localhost:{listenerPort}");

        // Configuration
        builder.Configuration
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Register all services
        builder.Services.AddVirtualAssistantServices(builder.Configuration);

        _app = builder.Build();

        // Add correlation ID middleware (must be early in pipeline)
        _app.UseMiddleware<CorrelationIdMiddleware>();

        // Apply migrations and configure endpoints
        _app.ApplyDatabaseMigrations();
        _app.MapVirtualAssistantEndpoints();

        // Get tray icon service from DI
        _trayService = _app.Services.GetRequiredService<VirtualAssistantTrayService>();

        try
        {
            await RunApplicationAsync(listenerPort);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
        finally
        {
            Cleanup();
        }

        Console.WriteLine("VirtualAssistant stopped");
    }

    private static async Task RunApplicationAsync(int listenerPort)
    {
        // Initialize tray icon (async, non-blocking)
        await _trayService!.InitializeAsync();
        Console.WriteLine("Tray icon initialized");
        Console.WriteLine($"API listening on http://localhost:{listenerPort}");

        // Subscribe to quit event
        _trayService.OnQuitRequested += OnQuitRequested;

        // Handle Ctrl+C
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nCtrl+C pressed - shutting down...");
            OnQuitRequested();
        };

        Console.WriteLine("VirtualAssistant running - tray icon active");
        Console.WriteLine("Press Ctrl+C or use tray menu to exit");
        Console.WriteLine();

        // Run WebApplication (blocks until cancellation)
        try
        {
            await _app!.RunAsync(_cts!.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private static void PrintBanner()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                   VirtualAssistant Service                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static void OnQuitRequested()
    {
        Console.WriteLine("Quit requested - stopping services...");
        _cts?.Cancel();
    }

    private static void Cleanup()
    {
        _trayService?.Dispose();
        _app?.DisposeAsync().AsTask().Wait();
        _cts?.Dispose();
        ReleaseSingleInstanceLock();
    }

    #region Single Instance Lock

    private static bool TryAcquireSingleInstanceLock()
    {
        try
        {
            _lockFile = new FileStream(
                _lockFilePath,
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

    private static void ReleaseSingleInstanceLock()
    {
        try
        {
            _lockFile?.Dispose();
            _lockFile = null;

            if (File.Exists(_lockFilePath))
            {
                File.Delete(_lockFilePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
