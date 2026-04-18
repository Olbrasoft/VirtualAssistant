using System.Diagnostics;
using System.Reflection;

namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <inheritdoc />
public sealed class DashboardMenuHandler : IDashboardMenuHandler
{
    private readonly ILogger<DashboardMenuHandler> _logger;
    private readonly string _dashboardBaseUrl;

    public DashboardMenuHandler(
        ILogger<DashboardMenuHandler> logger,
        string? dashboardBaseUrl)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Nullable on purpose: config wiring in TrayServicesExtensions pulls
        // the URL from IConfiguration and passes null when the
        // "Dashboard:BaseUrl" key is absent. Normalize null to empty string so
        // HandleDashboard can log a clear "not configured" warning instead of
        // reaching for a hardcoded fallback (#984 — no hardcoded localhost).
        _dashboardBaseUrl = dashboardBaseUrl ?? string.Empty;
    }

    public void HandleDashboard()
    {
        if (string.IsNullOrWhiteSpace(_dashboardBaseUrl))
        {
            _logger.LogWarning("Dashboard:BaseUrl is not configured; tray 'Dashboard' click is a no-op");
            return;
        }

        try
        {
            var dashboardUrl = $"{_dashboardBaseUrl}/Admin";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = dashboardUrl,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            _logger.LogInformation("Opened dashboard at {Url}", dashboardUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open dashboard");
        }
    }

    public void HandleAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        _logger.LogInformation("About requested - Version: {Version}", version);

        try
        {
            var text = $"<b>VirtualAssistant</b>\\n\\nVerze: {version}\\n\\nLinux virtuální asistent pro ovládání desktopu a integraci s AI coding agenty.\\n\\nhttps://github.com/Olbrasoft/VirtualAssistant";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "zenity",
                    Arguments = $"--info --title=\"O aplikaci\" --text=\"{text}\" --no-wrap --ok-label=\"Zavřít\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            _logger.LogInformation("Showed About dialog (zenity) - Version: {Version}", version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show about dialog using zenity");
        }
    }
}
