using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Extensions;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;

namespace Olbrasoft.VirtualAssistant.Service;

public class Program
{
    private static WebApplication? _app;
    private static TrayCoordinatorService? _trayService;
    private static CancellationTokenSource? _cts;
    private static ISingleInstanceLockManager? _lockManager;

    public static async Task Main(string[] args)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "../config/appsettings.json");
        var earlyConfig = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var lockFilePath = earlyConfig["SystemPaths:VirtualAssistantLockFile"]
            ?? "/tmp/virtual-assistant.lock";

        _lockManager = new SingleInstanceLockManager(lockFilePath);

        if (!_lockManager.TryAcquire())
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

        // Configure Kestrel - bind to all interfaces for network access
        var listenerPort = builder.Configuration.GetValue("ListenerApiPort", 5055);
        // SECURITY WARNING: Binding to 0.0.0.0 exposes the service to all network interfaces,
        // including external networks. This service currently has NO authentication.
        // For production use on untrusted networks, consider:
        // 1. Binding to localhost only (127.0.0.1) for local-only access
        // 2. Adding authentication middleware (API keys, OAuth, etc.)
        // 3. Using a reverse proxy (nginx) with access control
        // 4. Configuring firewall rules to restrict access
        builder.WebHost.UseUrls($"http://0.0.0.0:{listenerPort}");

        // Configuration
        builder.Configuration
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Add SecureStore for encrypted secrets (Google TTS API keys, etc.)
        // Secrets are stored in ~/.config/virtual-assistant/secrets/secrets.json
        // Key file is at ~/.config/virtual-assistant/keys/secrets.key
        var secureStoreConfig = builder.Configuration.GetSection("SecureStore");
        var secretsPath = secureStoreConfig["SecretsPath"] ?? "~/.config/virtual-assistant/secrets/secrets.json";
        var keyPath = secureStoreConfig["KeyPath"] ?? "~/.config/virtual-assistant/keys/secrets.key";
        builder.Configuration.AddSecureStore(secretsPath, keyPath);

        // Register all services
        builder.Services.AddVirtualAssistantServices(builder.Configuration);

        _app = builder.Build();

        // Enable default files (index.html) and static files (wwwroot) before endpoint mapping
        _app.UseDefaultFiles();
        _app.UseStaticFiles();

        // Apply migrations and configure endpoints
        _app.ApplyDatabaseMigrations();
        _app.MapVirtualAssistantEndpoints();

        // Get tray icon service from DI
        _trayService = _app.Services.GetRequiredService<TrayCoordinatorService>();

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

        // NOTE: DependentServicesManager removed - TTS runs inline (issue #407)

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

        // NOTE: DependentServicesManager removed - TTS runs inline (issue #407)

        _cts?.Cancel();
    }

    private static void Cleanup()
    {
        _trayService?.Dispose();
        _app?.DisposeAsync().AsTask().Wait();
        _cts?.Dispose();
        _lockManager?.Dispose();
    }
}
