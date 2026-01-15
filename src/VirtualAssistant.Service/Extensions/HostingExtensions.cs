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
    private static void ConfigureKestrel(this WebApplicationBuilder builder)
    {
        var listenerPort = builder.Configuration.GetValue("ListenerApiPort", 5055);

        // SECURITY WARNING: Binding to 0.0.0.0 exposes the service to all network interfaces,
        // including external networks. This service currently has NO authentication.
        // For production use on untrusted networks, consider:
        // 1. Binding to localhost only (127.0.0.1) for local-only access
        // 2. Adding authentication middleware (API keys, OAuth, etc.)
        // 3. Using a reverse proxy (nginx) with access control
        // 4. Configuring firewall rules to restrict access
        builder.WebHost.UseUrls($"http://0.0.0.0:{listenerPort}");
    }

    /// <summary>
    /// Configures the WebApplication pipeline with middleware and endpoints.
    /// </summary>
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();
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
