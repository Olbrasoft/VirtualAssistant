using Microsoft.Extensions.Logging;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.LinuxDesktop.DBus.Services;
using Olbrasoft.VirtualAssistant.Core.Services;
using Tmds.DBus.Protocol;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Service for querying cursor position via GNOME Shell extension.
/// Implements graceful degradation when extension is unavailable.
/// </summary>
public class CursorPositionService : ICursorPositionService, IAsyncDisposable
{
    private readonly ILogger<CursorPositionService> _logger;
    private IPointerService? _pointerService;
    private volatile bool _initialized;
    private volatile bool _initializationFailed;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile bool _disposed;

    public CursorPositionService(ILogger<CursorPositionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _initialized || _initializationFailed) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_disposed || _initialized || _initializationFailed) return;

            try
            {
                _pointerService = await PointerService.CreateAsync(null, cancellationToken);
                _initialized = true;
                _logger.LogInformation("CursorPositionService initialized successfully");
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                _logger.LogWarning(ex, "Failed to initialize CursorPositionService - cursor position will be unavailable");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<(int X, int Y)?> GetCursorPositionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return null;

        await EnsureInitializedAsync(cancellationToken);

        if (_pointerService == null)
        {
            _logger.LogDebug("PointerService not available, returning null");
            return null;
        }

        try
        {
            var position = await _pointerService.GetPointerPositionAsync(cancellationToken);

            if (position.HasValue)
            {
                _logger.LogDebug("Got cursor position: ({X}, {Y})", position.Value.X, position.Value.Y);
            }

            return position;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cursor position");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<(int X, int Y, int Width, int Height)?> GetActiveWindowGeometryAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return null;

        await EnsureInitializedAsync(cancellationToken);

        if (_pointerService == null)
        {
            _logger.LogDebug("PointerService not available, returning null");
            return null;
        }

        try
        {
            var geometry = await _pointerService.GetActiveWindowGeometryAsync(cancellationToken);

            if (geometry.HasValue)
            {
                _logger.LogDebug("Got window geometry: ({X}, {Y}, {Width}, {Height})",
                    geometry.Value.X, geometry.Value.Y, geometry.Value.Width, geometry.Value.Height);
            }

            return geometry;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get active window geometry");
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _lock.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;

            if (_pointerService != null)
            {
                await _pointerService.DisposeAsync();
                _pointerService = null;
            }

            _logger.LogDebug("CursorPositionService disposed");
        }
        finally
        {
            _lock.Release();
        }

        // Dispose semaphore outside the lock to avoid ObjectDisposedException
        _lock.Dispose();
    }
}
