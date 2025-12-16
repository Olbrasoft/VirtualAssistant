using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// System tray icon service using GTK3 and AppIndicator.
/// Runs on the main thread with GTK main loop.
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly IManualMuteService _muteService;
    private readonly ILogger<TrayIconService> _logger;
    private readonly int _logViewerPort;
    private IntPtr _indicator;
    private IntPtr _muteMenuItem;
    
    // Keep callbacks alive to prevent GC
    private GObject.GCallback? _muteCallback;
    private GObject.GCallback? _logsCallback;
    private GObject.GCallback? _quitCallback;
    private GLib.GSourceFunc? _updateMuteCallback;
    
    private bool _isInitialized;
    private Action? _onQuitRequested;

    public TrayIconService(IManualMuteService muteService, ILogger<TrayIconService> logger, int logViewerPort = 5053)
    {
        _muteService = muteService;
        _logger = logger;
        _logViewerPort = logViewerPort;

        // Subscribe to mute changes to update menu
        _muteService.MuteStateChanged += OnMuteStateChanged;
    }

    /// <summary>
    /// Initializes GTK and creates the tray icon.
    /// Must be called from main thread before starting GTK main loop.
    /// </summary>
    public void Initialize(Action onQuitRequested)
    {
        _onQuitRequested = onQuitRequested;

        // Initialize GTK
        int argc = 0;
        IntPtr argv = IntPtr.Zero;
        Gtk.gtk_init(ref argc, ref argv);

        // Get icon path
        string iconPath = Path.Combine(AppContext.BaseDirectory, "icons");

        // Create app indicator with initial icon based on mute state
        var initialIcon = _muteService.IsMuted ? "virtual-assistant-muted" : "virtual-assistant-active";
        _indicator = AppIndicator.app_indicator_new(
            "virtual-assistant-service",
            initialIcon,
            AppIndicator.Category.ApplicationStatus);

        if (_indicator == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create app indicator");
        }

        AppIndicator.app_indicator_set_icon_theme_path(_indicator, iconPath);
        AppIndicator.app_indicator_set_title(_indicator, "VirtualAssistant");
        AppIndicator.app_indicator_set_status(_indicator, AppIndicator.Status.Active);

        // Create menu
        var menu = Gtk.gtk_menu_new();

        // Status item (disabled)
        var statusItem = Gtk.gtk_menu_item_new_with_label("VirtualAssistant - poslouchám");
        Gtk.gtk_widget_set_sensitive(statusItem, false);
        Gtk.gtk_menu_shell_append(menu, statusItem);

        // Separator
        var separator1 = Gtk.gtk_separator_menu_item_new();
        Gtk.gtk_menu_shell_append(menu, separator1);

        // Mute/Unmute item
        _muteMenuItem = Gtk.gtk_menu_item_new_with_label(GetMuteLabel());
        Gtk.gtk_menu_shell_append(menu, _muteMenuItem);

        _muteCallback = (widget, data) =>
        {
            _muteService.Toggle();
        };
        GObject.g_signal_connect_data(_muteMenuItem, "activate", _muteCallback, IntPtr.Zero, IntPtr.Zero, 0);

        // Show Logs item
        var logsItem = Gtk.gtk_menu_item_new_with_label("Zobrazit logy");
        Gtk.gtk_menu_shell_append(menu, logsItem);

        _logsCallback = (widget, data) =>
        {
            OpenLogsInBrowser();
        };
        GObject.g_signal_connect_data(logsItem, "activate", _logsCallback, IntPtr.Zero, IntPtr.Zero, 0);

        // Separator
        var separator2 = Gtk.gtk_separator_menu_item_new();
        Gtk.gtk_menu_shell_append(menu, separator2);

        // Quit item
        var quitItem = Gtk.gtk_menu_item_new_with_label("Ukončit");
        Gtk.gtk_menu_shell_append(menu, quitItem);

        _quitCallback = (widget, data) =>
        {
            _onQuitRequested?.Invoke();
            Gtk.gtk_main_quit();
        };
        GObject.g_signal_connect_data(quitItem, "activate", _quitCallback, IntPtr.Zero, IntPtr.Zero, 0);

        Gtk.gtk_widget_show_all(menu);
        AppIndicator.app_indicator_set_menu(_indicator, menu);

        _isInitialized = true;
    }

    /// <summary>
    /// Runs the GTK main loop. This blocks until gtk_main_quit is called.
    /// </summary>
    public void RunMainLoop()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("TrayIconService not initialized");

        Gtk.gtk_main();
    }

    /// <summary>
    /// Quits the GTK main loop from another thread.
    /// </summary>
    public void QuitMainLoop()
    {
        if (_isInitialized)
        {
            // Use g_idle_add to safely call gtk_main_quit from any thread
            GLib.g_idle_add(_ =>
            {
                Gtk.gtk_main_quit();
                return false;
            }, IntPtr.Zero);
        }
    }

    private void OnMuteStateChanged(object? sender, bool isMuted)
    {
        // Update menu label and icon from GTK thread
        _updateMuteCallback = _ =>
        {
            if (_muteMenuItem != IntPtr.Zero)
            {
                Gtk.gtk_menu_item_set_label(_muteMenuItem, GetMuteLabel());
            }
            
            // Change icon based on mute state
            if (_indicator != IntPtr.Zero)
            {
                var iconName = isMuted ? "virtual-assistant-muted" : "virtual-assistant-active";
                AppIndicator.app_indicator_set_icon(_indicator, iconName);
            }
            
            return false; // Don't repeat
        };
        GLib.g_idle_add(_updateMuteCallback, IntPtr.Zero);
    }

    private string GetMuteLabel()
    {
        return _muteService.IsMuted ? "🔊 Zapnout mikrofon" : "🔇 Ztlumit mikrofon";
    }

    private void OpenLogsInBrowser()
    {
        try
        {
            var url = $"http://localhost:{_logViewerPort}";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            // Fire-and-forget: xdg-open spawns browser and exits immediately
            // Using statement ensures Process resources are released
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open logs");
        }
    }

    public void Dispose()
    {
        _muteService.MuteStateChanged -= OnMuteStateChanged;
    }
}
