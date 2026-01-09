using Microsoft.Extensions.Logging;
using Tmds.DBus;
using Olbrasoft.VirtualAssistant.Desktop.DBus;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Shows recording/transcribing status via D-Bus desktop notifications.
/// Implements Phase 1 of Recording UI Overlay (#670).
/// </summary>
public class RecordingNotificationService : IRecordingNotificationService
{
    private readonly ILogger<RecordingNotificationService> _logger;
    private Connection? _connection;
    private INotifications? _notifications;
    private uint _currentNotificationId;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile bool _initialized;
    private volatile bool _initializationFailed;
    private volatile bool _disposed;

    private const string ServiceName = "org.freedesktop.Notifications";
    private const string ObjectPath = "/org/freedesktop/Notifications";
    private const string AppName = "VirtualAssistant";

    public RecordingNotificationService(ILogger<RecordingNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        if (_initializationFailed) return; // Silent fail after first attempt

        await _lock.WaitAsync(ct);
        try
        {
            if (_initialized || _initializationFailed) return;

            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();

            _notifications = _connection.CreateProxy<INotifications>(
                ServiceName,
                new ObjectPath(ObjectPath)
            );

            _initialized = true;
            _logger.LogInformation("RecordingNotificationService initialized successfully");
        }
        catch (Exception ex)
        {
            _connection?.Dispose();
            _connection = null;
            _notifications = null;
            _initializationFailed = true;
            _logger.LogWarning(ex, "Failed to initialize RecordingNotificationService - notifications will be disabled");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ShowRecordingAsync(CancellationToken ct = default)
    {
        await ShowNotificationAsync(
            summary: "🟠 Nahrávání",
            body: "Diktujte text...",
            ct);
    }

    public async Task ShowTranscribingAsync(CancellationToken ct = default)
    {
        await ShowNotificationAsync(
            summary: "⏳ Přepis",
            body: "Probíhá transkripce...",
            ct);
    }

    public async Task HideAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_initialized || _notifications == null) return;

            if (_currentNotificationId != 0)
            {
                try
                {
                    await _notifications.CloseNotificationAsync(_currentNotificationId);
                    _logger.LogDebug("Closed notification {Id}", _currentNotificationId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to close notification {Id} (may have already expired)", _currentNotificationId);
                }
                finally
                {
                    _currentNotificationId = 0;
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ShowNotificationAsync(string summary, string body, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        await _lock.WaitAsync(ct);
        try
        {
            if (!_initialized || _notifications == null)
            {
                _logger.LogDebug("Notifications not available, skipping: {Summary}", summary);
                return;
            }
            var hints = new Dictionary<string, object>
            {
                // Use "urgency" hint for notification priority (0=low, 1=normal, 2=critical)
                ["urgency"] = (byte)1,
                // Transient notification - don't persist in notification center
                ["transient"] = true
            };

            _currentNotificationId = await _notifications.NotifyAsync(
                appName: AppName,
                replacesId: _currentNotificationId, // Replace existing if any
                appIcon: "audio-input-microphone", // Standard icon
                summary: summary,
                body: body,
                actions: Array.Empty<string>(),
                hints: hints,
                expireTimeout: 0 // Persist until explicitly closed
            );

            _logger.LogDebug("Showed notification {Id}: {Summary}", _currentNotificationId, summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show notification: {Summary}", summary);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;

            // Try to close any active notification
            if (_initialized && _notifications != null && _currentNotificationId != 0)
            {
                try
                {
                    await _notifications.CloseNotificationAsync(_currentNotificationId);
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }

            _notifications = null;
            _connection?.Dispose();
            _connection = null;
            _logger.LogDebug("RecordingNotificationService disposed");
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
