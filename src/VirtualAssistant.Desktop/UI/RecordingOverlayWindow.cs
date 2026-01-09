using Gtk;
using Microsoft.Extensions.Logging;
using ZwlrLayerShell;

namespace Olbrasoft.VirtualAssistant.Desktop.UI;

/// <summary>
/// GTK4 Layer Shell overlay window for displaying recording/transcribing status.
/// Shows near cursor position on Wayland compositor with layer shell support.
/// </summary>
public class RecordingOverlayWindow : IRecordingOverlayWindow
{
    private readonly ILogger _logger;
    private Application? _application;
    private Window? _window;
    private Label? _statusLabel;
    private Box? _container;
    private bool _isVisible;
    private bool _disposed;
    private Thread? _gtkThread;
    private readonly ManualResetEventSlim _initialized = new(false);
    private readonly object _lock = new();

    // GNOME privacy indicator orange: #ff7800
    private const string OrangeColor = "#ff7800";

    public RecordingOverlayWindow(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes GTK and creates the overlay window.
    /// Must be called before Show/Hide methods.
    /// </summary>
    public void Initialize()
    {
        if (_disposed) return;

        _gtkThread = new Thread(GtkMain)
        {
            Name = "GTK4-Overlay",
            IsBackground = true
        };
        _gtkThread.Start();

        // Wait for GTK initialization
        if (!_initialized.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger.LogWarning("GTK initialization timeout - overlay may not work");
        }
    }

    private void GtkMain()
    {
        try
        {
            _application = Application.New("cz.olbrasoft.recording-overlay", Gio.ApplicationFlags.FlagsNone);

            _application.OnActivate += (sender, args) =>
            {
                CreateWindow((Application)sender!);
                _initialized.Set();
            };

            _application.RunWithSynchronizationContext(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GTK main loop failed");
            _initialized.Set(); // Unblock waiting thread
        }
    }

    private void CreateWindow(Application app)
    {
        _window = ApplicationWindow.New(app);

        // Check Layer Shell support
        if (!LayerShell.IsSupported())
        {
            _logger.LogWarning("Layer Shell not supported by compositor - overlay positioning limited");
        }
        else
        {
            // Initialize as layer shell surface
            LayerShell.InitForWindow(_window);
            LayerShell.SetNamespace(_window, "recording-overlay");
            LayerShell.SetLayer(_window, Layer.Overlay); // Always on top
            LayerShell.SetKeyboardMode(_window, KeyboardMode.None); // Don't grab keyboard

            // Don't anchor - we'll position manually
            LayerShell.SetAnchor(_window, Edge.Top, false);
            LayerShell.SetAnchor(_window, Edge.Bottom, false);
            LayerShell.SetAnchor(_window, Edge.Left, false);
            LayerShell.SetAnchor(_window, Edge.Right, false);

            // Set exclusive zone to -1 to not reserve space
            LayerShell.SetExclusiveZone(_window, -1);
        }

        // Window properties
        _window.SetDecorated(false);
        _window.SetDefaultSize(180, 40);

        // Create UI
        _container = Box.New(Orientation.Horizontal, 8);
        _container.SetMarginStart(12);
        _container.SetMarginEnd(12);
        _container.SetMarginTop(8);
        _container.SetMarginBottom(8);

        // Orange indicator dot
        var indicator = new DrawingArea();
        indicator.SetSizeRequest(12, 12);
        indicator.SetDrawFunc(DrawIndicator);

        // Status label
        _statusLabel = Label.New("Recording...");
        _statusLabel.AddCssClass("status-label");

        _container.Append(indicator);
        _container.Append(_statusLabel);

        _window.SetChild(_container);

        // Apply CSS styling
        ApplyCss();

        // Start hidden
        _window.SetVisible(false);

        _logger.LogDebug("Recording overlay window created");
    }

    private void DrawIndicator(DrawingArea area, Cairo.Context cr, int width, int height)
    {
        // Orange dot (#ff7800 = RGB 255, 120, 0)
        cr.SetSourceRgba(1.0, 0.47, 0.0, 1.0);
        cr.Arc(width / 2.0, height / 2.0, 5, 0, 2 * Math.PI);
        cr.Fill();
    }

    private void ApplyCss()
    {
        var cssProvider = CssProvider.New();
        cssProvider.LoadFromString(@"
            window {
                background-color: rgba(40, 40, 40, 0.95);
                border-radius: 8px;
                border: 1px solid rgba(255, 120, 0, 0.5);
            }
            .status-label {
                color: white;
                font-weight: bold;
                font-size: 14px;
            }
        ");

        if (_window != null)
        {
            var display = _window.GetDisplay();
            StyleContext.AddProviderForDisplay(display, cssProvider, 800);
        }
    }

    /// <summary>
    /// Shows the overlay with "Recording..." text at specified position.
    /// </summary>
    public void ShowRecording(int x, int y)
    {
        lock (_lock)
        {
            if (_disposed || _window == null) return;

            GLib.Functions.IdleAdd(0, () =>
            {
                UpdatePosition(x, y);
                _statusLabel?.SetText("Recording...");
                _window?.SetVisible(true);
                _isVisible = true;
                return false; // Don't repeat
            });

            _logger.LogDebug("Showing recording overlay at ({X}, {Y})", x, y);
        }
    }

    /// <summary>
    /// Shows the overlay with "Transcribing..." text at specified position.
    /// </summary>
    public void ShowTranscribing(int x, int y)
    {
        lock (_lock)
        {
            if (_disposed || _window == null) return;

            GLib.Functions.IdleAdd(0, () =>
            {
                UpdatePosition(x, y);
                _statusLabel?.SetText("Transcribing...");
                _window?.SetVisible(true);
                _isVisible = true;
                return false;
            });

            _logger.LogDebug("Showing transcribing overlay at ({X}, {Y})", x, y);
        }
    }

    /// <summary>
    /// Hides the overlay.
    /// </summary>
    public void Hide()
    {
        lock (_lock)
        {
            if (_disposed || _window == null || !_isVisible) return;

            GLib.Functions.IdleAdd(0, () =>
            {
                _window?.SetVisible(false);
                _isVisible = false;
                return false;
            });

            _logger.LogDebug("Hiding recording overlay");
        }
    }

    /// <summary>
    /// Updates overlay position (20px above cursor).
    /// </summary>
    public void UpdatePosition(int cursorX, int cursorY)
    {
        if (_window == null || !LayerShell.IsSupported()) return;

        // Position overlay 20px above cursor, centered horizontally
        var overlayWidth = 180;
        var overlayHeight = 40;
        var x = cursorX - overlayWidth / 2;
        var y = cursorY - overlayHeight - 20;

        // Ensure within screen bounds (basic check)
        if (x < 0) x = 0;
        if (y < 0) y = 100; // Fallback to top area

        LayerShell.SetMargin(_window, Edge.Left, x);
        LayerShell.SetMargin(_window, Edge.Top, y);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        GLib.Functions.IdleAdd(0, () =>
        {
            _window?.Close();
            _application?.Quit();
            return false;
        });

        _initialized.Dispose();
        _logger.LogDebug("RecordingOverlayWindow disposed");
    }
}
