using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Recording overlay service implementation.
/// Currently delegates to IRecordingNotificationService (Phase 1).
/// GTK4 LayerShell overlay can be added in a future iteration.
/// </summary>
/// <remarks>
/// Future implementation could use GTK4 Layer Shell for positioned overlay:
/// - libgtk4-layer-shell for Wayland overlay
/// - Position near cursor using ICursorPositionService
/// - Blinking animation for recording state
/// </remarks>
public class RecordingOverlayService : IRecordingOverlayService
{
    private readonly ILogger<RecordingOverlayService> _logger;
    private readonly IRecordingNotificationService _notificationService;
    private readonly ICursorPositionService _cursorPositionService;
    private bool _disposed;

    public RecordingOverlayService(
        ILogger<RecordingOverlayService> logger,
        IRecordingNotificationService notificationService,
        ICursorPositionService cursorPositionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _cursorPositionService = cursorPositionService ?? throw new ArgumentNullException(nameof(cursorPositionService));
    }

    /// <inheritdoc/>
    public async Task ShowRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        _logger.LogDebug("Showing recording overlay");

        // Log cursor position for future positioning
        var position = await _cursorPositionService.GetCursorPositionAsync(cancellationToken);
        if (position.HasValue)
        {
            _logger.LogDebug("Cursor at ({X}, {Y}) - overlay would be positioned here",
                position.Value.X, position.Value.Y);
        }

        // Delegate to notification service (Phase 1 fallback)
        await _notificationService.ShowRecordingAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ShowTranscribingAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        _logger.LogDebug("Showing transcribing overlay");

        // Delegate to notification service (Phase 1 fallback)
        await _notificationService.ShowTranscribingAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task HideAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        _logger.LogDebug("Hiding overlay");

        // Delegate to notification service (Phase 1 fallback)
        await _notificationService.HideAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdatePositionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        // Position tracking for future GTK4 overlay implementation
        var position = await _cursorPositionService.GetCursorPositionAsync(cancellationToken);
        if (position.HasValue)
        {
            _logger.LogDebug("Would update overlay position to ({X}, {Y})",
                position.Value.X, position.Value.Y);
        }

        // Current notification-based implementation doesn't support positioning
        // GTK4 LayerShell implementation would move the window here
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _logger.LogDebug("RecordingOverlayService disposed");
        return ValueTask.CompletedTask;
    }
}
