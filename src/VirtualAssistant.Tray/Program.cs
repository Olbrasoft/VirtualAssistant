using System.Runtime.InteropServices;

namespace VirtualAssistant.Tray;

/// <summary>
/// P/Invoke bindings for libayatana-appindicator3
/// </summary>
internal static class AppIndicator
{
    private const string LibName = "libayatana-appindicator3.so.1";

    public enum Category
    {
        ApplicationStatus = 0,
        Communications = 1,
        SystemServices = 2,
        Hardware = 3,
        Other = 4
    }

    public enum Status
    {
        Passive = 0,
        Active = 1,
        Attention = 2
    }

    [DllImport(LibName)]
    public static extern IntPtr app_indicator_new(string id, string icon_name, Category category);

    [DllImport(LibName)]
    public static extern void app_indicator_set_status(IntPtr indicator, Status status);

    [DllImport(LibName)]
    public static extern void app_indicator_set_menu(IntPtr indicator, IntPtr menu);

    [DllImport(LibName)]
    public static extern void app_indicator_set_icon_theme_path(IntPtr indicator, string icon_theme_path);

    [DllImport(LibName)]
    public static extern void app_indicator_set_title(IntPtr indicator, string title);
}

/// <summary>
/// P/Invoke bindings for GTK3
/// </summary>
internal static class Gtk
{
    private const string LibName = "libgtk-3.so.0";

    [DllImport(LibName)]
    public static extern void gtk_init(ref int argc, ref IntPtr argv);

    [DllImport(LibName)]
    public static extern void gtk_main();

    [DllImport(LibName)]
    public static extern void gtk_main_quit();

    [DllImport(LibName)]
    public static extern IntPtr gtk_menu_new();

    [DllImport(LibName)]
    public static extern IntPtr gtk_menu_item_new_with_label(string label);

    [DllImport(LibName)]
    public static extern IntPtr gtk_separator_menu_item_new();

    [DllImport(LibName)]
    public static extern void gtk_menu_shell_append(IntPtr menu_shell, IntPtr child);

    [DllImport(LibName)]
    public static extern void gtk_widget_show_all(IntPtr widget);

    [DllImport(LibName)]
    public static extern void gtk_widget_set_sensitive(IntPtr widget, bool sensitive);
}

/// <summary>
/// P/Invoke bindings for GLib
/// </summary>
internal static class GLib
{
    private const string LibName = "libglib-2.0.so.0";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool GSourceFunc(IntPtr data);

    [DllImport(LibName)]
    public static extern uint g_idle_add(GSourceFunc function, IntPtr data);
}

/// <summary>
/// P/Invoke bindings for GObject
/// </summary>
internal static class GObject
{
    private const string LibName = "libgobject-2.0.so.0";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void GCallback(IntPtr widget, IntPtr data);

    [DllImport(LibName)]
    public static extern ulong g_signal_connect_data(
        IntPtr instance,
        string detailed_signal,
        GCallback c_handler,
        IntPtr data,
        IntPtr destroy_data,
        int connect_flags);
}

class Program
{
    private static GObject.GCallback? _quitCallback;
    private static GObject.GCallback? _sayNotificationCallback;
    private static GObject.GCallback? _aboutCallback;
    private static GLib.GSourceFunc? _startupTtsCallback;
    private static TtsService? _ttsService;

    private static string GetVersion()
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var informationalVersion = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;
        return informationalVersion ?? version;
    }

    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("VirtualAssistant Tray starting...");

        try
        {
            // Initialize TTS service
            _ttsService = new TtsService();

            // Initialize GTK
            int argc = 0;
            IntPtr argv = IntPtr.Zero;
            Gtk.gtk_init(ref argc, ref argv);
            Console.WriteLine("GTK initialized");

            // Get icon path
            string iconPath = Path.Combine(AppContext.BaseDirectory, "icons");
            Console.WriteLine($"Icon path: {iconPath}");

            // Create app indicator
            var indicator = AppIndicator.app_indicator_new(
                "virtual-assistant-tray",
                "virtual-assistant-muted",
                AppIndicator.Category.ApplicationStatus);

            if (indicator == IntPtr.Zero)
            {
                Console.WriteLine("Error: Failed to create app indicator");
                Environment.Exit(1);
            }

            AppIndicator.app_indicator_set_icon_theme_path(indicator, iconPath);
            AppIndicator.app_indicator_set_title(indicator, "VirtualAssistant");
            AppIndicator.app_indicator_set_status(indicator, AppIndicator.Status.Active);
            Console.WriteLine("App indicator created");

            // Create menu
            var menu = Gtk.gtk_menu_new();

            // Status item (disabled)
            var statusItem = Gtk.gtk_menu_item_new_with_label("VirtualAssistant běží");
            Gtk.gtk_widget_set_sensitive(statusItem, false);
            Gtk.gtk_menu_shell_append(menu, statusItem);

            // Separator
            var separator1 = Gtk.gtk_separator_menu_item_new();
            Gtk.gtk_menu_shell_append(menu, separator1);

            // Say notification item
            var sayNotificationItem = Gtk.gtk_menu_item_new_with_label("Řekni notifikaci");
            Gtk.gtk_menu_shell_append(menu, sayNotificationItem);

            // Connect say notification signal
            _sayNotificationCallback = (widget, data) =>
            {
                Console.WriteLine("Say notification requested via menu");
                Task.Run(async () => await _ttsService!.SpeakAsync("Úkol dokončen."));
            };
            GObject.g_signal_connect_data(sayNotificationItem, "activate", _sayNotificationCallback, IntPtr.Zero, IntPtr.Zero, 0);

            // About item
            var aboutItem = Gtk.gtk_menu_item_new_with_label("O aplikaci");
            Gtk.gtk_menu_shell_append(menu, aboutItem);

            // Connect about signal
            _aboutCallback = (widget, data) =>
            {
                var version = GetVersion();
                Console.WriteLine($"VirtualAssistant verze {version}");
                Task.Run(async () => await _ttsService!.SpeakAsync($"Virtual Assistant verze {version}"));
            };
            GObject.g_signal_connect_data(aboutItem, "activate", _aboutCallback, IntPtr.Zero, IntPtr.Zero, 0);

            // Separator
            var separator2 = Gtk.gtk_separator_menu_item_new();
            Gtk.gtk_menu_shell_append(menu, separator2);

            // Quit item
            var quitItem = Gtk.gtk_menu_item_new_with_label("Ukončit");
            Gtk.gtk_menu_shell_append(menu, quitItem);

            // Connect quit signal
            _quitCallback = (widget, data) =>
            {
                Console.WriteLine("Quit requested via menu");
                _ttsService?.Dispose();
                Gtk.gtk_main_quit();
            };
            GObject.g_signal_connect_data(quitItem, "activate", _quitCallback, IntPtr.Zero, IntPtr.Zero, 0);

            Gtk.gtk_widget_show_all(menu);
            AppIndicator.app_indicator_set_menu(indicator, menu);
            Console.WriteLine("Menu created");

            // Schedule "Ahoj světe!" TTS after GTK main loop starts
            _startupTtsCallback = (data) =>
            {
                Task.Run(async () => await _ttsService!.SpeakAsync("Ahoj světe!"));
                return false; // Don't repeat
            };
            GLib.g_idle_add(_startupTtsCallback, IntPtr.Zero);

            // Handle Ctrl+C
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Ctrl+C pressed");
                _ttsService?.Dispose();
                Gtk.gtk_main_quit();
            };

            Console.WriteLine("VirtualAssistant tray icon is active");
            Console.WriteLine("Press Ctrl+C to exit or use menu 'Ukončit'");

            // Run GTK main loop
            Gtk.gtk_main();

            Console.WriteLine("VirtualAssistant Tray exiting...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
