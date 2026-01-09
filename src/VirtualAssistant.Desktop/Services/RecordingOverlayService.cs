using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Desktop.UI;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Recording overlay service implementation using GTK4 LayerShell.
/// Shows overlay window near cursor position during recording/transcribing.
/// </summary>
public class RecordingOverlayService : IRecordingOverlayService
{
    private readonly ILogger<RecordingOverlayService> _logger;
    private readonly ICursorPositionService _cursorPositionService;
    private readonly IRecordingOverlayWindow _overlayWindow;
    private bool _disposed;
    private bool _initialized;

    // Fallback position when cursor position unavailable
    private const int FallbackX = 100;
    private const int FallbackY = 100;

    public RecordingOverlayService(
        ILogger<RecordingOverlayService> logger,
        ICursorPositionService cursorPositionService,
        IRecordingOverlayWindow overlayWindow)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cursorPositionService = cursorPositionService ?? throw new ArgumentNullException(nameof(cursorPositionService));
        _overlayWindow = overlayWindow ?? throw new ArgumentNullException(nameof(overlayWindow));
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        try
        {
            _overlayWindow.Initialize();
            _initialized = true;
            _logger.LogInformation("RecordingOverlayService initialized with GTK4 LayerShell");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GTK4 overlay window");
        }
    }

    /// <inheritdoc/>
    public async Task ShowRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        EnsureInitialized();

        var (x, y) = await GetCursorPositionOrFallbackAsync(cancellationToken);

        _logger.LogDebug("Showing recording overlay at ({X}, {Y})", x, y);
        _overlayWindow.ShowRecording(x, y);
    }

    /// <inheritdoc/>
    public async Task ShowTranscribingAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        EnsureInitialized();

        var (x, y) = await GetCursorPositionOrFallbackAsync(cancellationToken);

        _logger.LogDebug("Showing transcribing overlay at ({X}, {Y})", x, y);
        _overlayWindow.ShowTranscribing(x, y);
    }

    /// <inheritdoc/>
    public Task HideAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;

        _logger.LogDebug("Hiding overlay");
        _overlayWindow.Hide();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task UpdatePositionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        var (x, y) = await GetCursorPositionOrFallbackAsync(cancellationToken);

        _logger.LogDebug("Updating overlay position to ({X}, {Y})", x, y);
        _overlayWindow.UpdatePosition(x, y);
    }

    private async Task<(int X, int Y)> GetCursorPositionOrFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            var position = await _cursorPositionService.GetCursorPositionAsync(cancellationToken);
            if (position.HasValue)
            {
                return (position.Value.X, position.Value.Y);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cursor position, using fallback");
        }

        return (FallbackX, FallbackY);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        _disposed = true;
        _overlayWindow.Dispose();
        _logger.LogDebug("RecordingOverlayService disposed");

        return ValueTask.CompletedTask;
    }
}
