using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for WebApplicationBuilder configuration.
/// Handles configuration loading, Kestrel setup, and service registration.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Configures the WebApplicationBuilder with all VirtualAssistant settings.
    /// </summary>
    public static WebApplicationBuilder ConfigureVirtualAssistant(
        this WebApplicationBuilder builder,
        string configPath)
    {
        builder.ConfigureConfiguration(configPath);
        builder.ConfigureKestrel();
        builder.Services.AddVirtualAssistantServices(builder.Configuration);

        return builder;
    }

    /// <summary>
    /// Configures application configuration with JSON file, environment variables, and SecureStore.
    /// </summary>
    private static void ConfigureConfiguration(this WebApplicationBuilder builder, string configPath)
    {
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
    }

    /// <summary>
    /// Configures Kestrel to listen on all interfaces.
    /// </summary>
    /// <remarks>
    /// **Auth policy (decided 2026-04-18 as part of code review #978):**
    ///
    /// The service binds to 0.0.0.0 because Remote Control must be reachable from
    /// the user's phone on the LAN. All non-webhook endpoints are therefore
    /// deliberately anonymous and rely on three layers of defense:
    ///
    /// 1. Trusted LAN. The user runs this service on a home/office network behind
    ///    NAT; the port is not internet-exposed. If you operate on an untrusted
    ///    network, configure a firewall rule or a reverse proxy with auth.
    ///
    /// 2. Input bounds on state-mutating endpoints (see e.g. NotificationsController
    ///    text-length / issue-id caps added under #984).
    ///
    /// 3. GitHub webhook endpoint has mandatory signature validation — see
    ///    GitHubWebhooksController (fail-closed behavior added under #978).
    ///
    /// If you add new state-mutating endpoints, either decorate them with
    /// [AllowAnonymous] and cite this block, or add a specific auth scheme.
    /// </remarks>
    private static void ConfigureKestrel(this WebApplicationBuilder builder)
    {
        var listenerPort = builder.Configuration.GetValue("ListenerApiPort", 5055);
        var remotePort = builder.Configuration.GetValue("RemoteControlPort", 5050);
        builder.WebHost.UseUrls($"http://0.0.0.0:{listenerPort}", $"http://0.0.0.0:{remotePort}");
    }

    /// <summary>
    /// Configures the WebApplication pipeline with middleware and endpoints.
    /// </summary>
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseDefaultFiles();
        // Note: UseStaticFiles is called in MapVirtualAssistantEndpoints
        app.ApplyDatabaseMigrations();
        app.MapVirtualAssistantEndpoints();

        return app;
    }

    /// <summary>
    /// Gets the configured listener port.
    /// </summary>
    public static int GetListenerPort(this WebApplication app)
    {
        return app.Configuration.GetValue("ListenerApiPort", 5055);
    }

    /// <summary>
    /// Prints the application banner to console.
    /// </summary>
    public static void PrintBanner()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                   VirtualAssistant Service                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    /// <summary>
    /// Tries to acquire single instance lock. Returns lock manager if successful, null otherwise.
    /// </summary>
    public static ISingleInstanceLockManager? TryAcquireSingleInstanceLock(string configPath)
    {
        var earlyConfig = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var lockFilePath = earlyConfig["SystemPaths:VirtualAssistantLockFile"]
            ?? "/tmp/virtual-assistant.lock";

        var lockManager = new SingleInstanceLockManager(lockFilePath);

        if (lockManager.TryAcquire())
            return lockManager;

        lockManager.Dispose();
        Console.WriteLine("ERROR: VirtualAssistant is already running!");
        Console.WriteLine("Only one instance is allowed.");
        return null;
    }

    /// <summary>
    /// Runs the application with tray service support.
    /// </summary>
    public static async Task RunWithTrayAsync(this WebApplication app)
    {
        using var cts = new CancellationTokenSource();
        var trayService = app.Services.GetRequiredService<TrayCoordinatorService>();

        await trayService.InitializeAsync();
        Console.WriteLine("Tray icon initialized");
        Console.WriteLine($"API listening on http://localhost:{app.GetListenerPort()}");

        trayService.OnQuitRequested += () =>
        {
            Console.WriteLine("Quit requested - stopping services...");
            cts.Cancel();
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nCtrl+C pressed - shutting down...");
            cts.Cancel();
        };

        Console.WriteLine("VirtualAssistant running - tray icon active");
        Console.WriteLine("Press Ctrl+C or use tray menu to exit");
        Console.WriteLine();

        try
        {
            await app.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }

        Console.WriteLine("VirtualAssistant stopped");
    }
}
