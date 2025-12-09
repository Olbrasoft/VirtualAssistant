using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Extensions;
using Olbrasoft.VirtualAssistant.Service.Tray;

namespace Olbrasoft.VirtualAssistant.Service;

/// <summary>
/// VirtualAssistant Service entry point.
/// Manages service lifecycle, tray icon, and single instance locking.
/// </summary>
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
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Register all services
        builder.Services.AddVirtualAssistantServices(builder.Configuration);

        _app = builder.Build();

        // Apply migrations and configure endpoints
        _app.ApplyDatabaseMigrations();
        _app.MapVirtualAssistantEndpoints();

        // Create tray icon service
        var muteService = _app.Services.GetRequiredService<IManualMuteService>();
        var options = _app.Services.GetRequiredService<IOptions<ContinuousListenerOptions>>();
        _trayService = new TrayIconService(muteService, options.Value.LogViewerPort);

        try
        {
            RunApplication(listenerPort);
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

    private static void RunApplication(int listenerPort)
    {
        // Initialize tray (must be on main thread)
        _trayService!.Initialize(OnQuitRequested);
        Console.WriteLine("Tray icon initialized");
        Console.WriteLine($"API listening on http://localhost:{listenerPort}");

        // Start WebApplication in background
        var hostTask = Task.Run(async () =>
        {
            try
            {
                await _app!.RunAsync(_cts!.Token);
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
            _trayService!.QuitMainLoop();
        };

        Console.WriteLine("VirtualAssistant running - tray icon active");
        Console.WriteLine("Press Ctrl+C or use tray menu to exit");
        Console.WriteLine();

        // Run GTK main loop (blocks until quit)
        _trayService.RunMainLoop();

        // Wait for host to finish
        hostTask.Wait(TimeSpan.FromSeconds(5));
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

    #endregion
}
