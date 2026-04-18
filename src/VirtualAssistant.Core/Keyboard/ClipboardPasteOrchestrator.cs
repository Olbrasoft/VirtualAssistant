using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Clipboard;

namespace Olbrasoft.VirtualAssistant.Core.Keyboard;

/// <inheritdoc />
public sealed class ClipboardPasteOrchestrator : IClipboardPasteOrchestrator
{
    private readonly IClipboardManager _clipboardManager;
    private readonly ILogger<ClipboardPasteOrchestrator> _logger;

    public ClipboardPasteOrchestrator(
        IClipboardManager clipboardManager,
        ILogger<ClipboardPasteOrchestrator> logger)
    {
        _clipboardManager = clipboardManager ?? throw new ArgumentNullException(nameof(clipboardManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> StageAndRestoreAsync(
        string stagedText,
        bool usePrimary,
        Func<Task<bool>> performPasteAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stagedText);
        ArgumentNullException.ThrowIfNull(performPasteAsync);

        var original = usePrimary
            ? await _clipboardManager.GetPrimarySelectionAsync(cancellationToken)
            : await _clipboardManager.GetClipboardAsync(cancellationToken);

        if (usePrimary)
            await _clipboardManager.SetPrimarySelectionAsync(stagedText, cancellationToken);
        else
            await _clipboardManager.SetClipboardAsync(stagedText, cancellationToken);

        try
        {
            return await performPasteAsync();
        }
        finally
        {
            await TryRestoreAsync(original, usePrimary, cancellationToken);
        }
    }

    private async Task TryRestoreAsync(string? original, bool usePrimary, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(original)) return;

        try
        {
            if (usePrimary)
                await _clipboardManager.SetPrimarySelectionAsync(original, cancellationToken);
            else
                await _clipboardManager.SetClipboardAsync(original, cancellationToken);

            _logger.LogDebug("Restored original {Selection}", usePrimary ? "PRIMARY" : "CLIPBOARD");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not restore {Selection}: {Message}",
                usePrimary ? "PRIMARY" : "CLIPBOARD", ex.Message);
        }
    }
}
