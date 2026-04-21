using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Clipboard;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Keyboard;

/// <summary>
/// Text typer implementation using clipboard + dotool for Linux Wayland/X11.
/// Uses a clipboard-based approach: saves current clipboard, copies text, pastes via dotool, then restores clipboard.
/// This approach supports full Unicode including Czech diacritics (háčky, čárky).
/// </summary>
public class XDoToolKeyboardService : IKeyboardSimulationService
{
    private static readonly TimeSpan PasteTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SequenceTimeout = TimeSpan.FromSeconds(10);

    // Defense-in-depth: paste shortcuts are never passed to a shell, but the
    // value is still used as dotool input. Keep the allowed set explicit so a
    // future refactor that changes the source of this value cannot slip in
    // something unexpected.
    private static readonly HashSet<string> AllowedPasteShortcuts = new(StringComparer.OrdinalIgnoreCase)
    {
        "ctrl+v", "ctrl+shift+v", "shift+insert",
    };

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "enter", "ctrl+u", "ctrl+v", "ctrl+shift+v", "shift+insert",
        "escape", "tab", "backspace", "delete", "alt+F4",
        "super+kp1", "super+kp2", "super+kp3", "super+kp4",
        "super+kp5", "super+kp6", "super+kp7", "super+kp8",
        "end", "ctrl+a",
    };

    private readonly IClipboardManager _clipboardManager;
    private readonly IClipboardPasteOrchestrator _pasteOrchestrator;
    private readonly IDotoolProcessRunner _dotoolRunner;
    private readonly ITerminalDetector _terminalDetector;
    private readonly ICliAppDetector _cliAppDetector;
    private readonly ITmuxCopyModeGuard _copyModeGuard;
    private readonly ILogger<XDoToolKeyboardService> _logger;

    public XDoToolKeyboardService(
        IClipboardManager clipboardManager,
        IClipboardPasteOrchestrator pasteOrchestrator,
        IDotoolProcessRunner dotoolRunner,
        ITerminalDetector terminalDetector,
        ICliAppDetector cliAppDetector,
        ITmuxCopyModeGuard copyModeGuard,
        ILogger<XDoToolKeyboardService> logger)
    {
        _clipboardManager = clipboardManager ?? throw new ArgumentNullException(nameof(clipboardManager));
        _pasteOrchestrator = pasteOrchestrator ?? throw new ArgumentNullException(nameof(pasteOrchestrator));
        _dotoolRunner = dotoolRunner ?? throw new ArgumentNullException(nameof(dotoolRunner));
        _terminalDetector = terminalDetector ?? throw new ArgumentNullException(nameof(terminalDetector));
        _cliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
        _copyModeGuard = copyModeGuard ?? throw new ArgumentNullException(nameof(copyModeGuard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Types text into the active window using clipboard + dotool paste.
    /// For terminal CLI agents (Claude Code, OpenCode, Gemini) the text is
    /// staged in the PRIMARY selection and pasted via Shift+Insert — these
    /// TUIs hijack Ctrl+Shift+V as a "paste image" shortcut, so the
    /// traditional X11 primary-paste route is the reliable one.
    /// </summary>
    public async Task<bool> TypeIntoActiveWindowAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Cannot type empty text");
            return false;
        }

        try
        {
            _logger.LogDebug("Typing text into active window: '{Text}' ({Length} chars)",
                text.Length > 50 ? text.Substring(0, 50) + "..." : text,
                text.Length);

            var textToType = text + " ";
            var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
            var usePrimary = pasteShortcut == "shift+insert";

            // #1050: if any active tmux pane is stuck in copy-mode (user wheel-
            // scrolled earlier), the upcoming Shift+Insert would be swallowed by
            // tmux's own bindings and never reach the CLI TUI. Exit copy-mode
            // first so the paste actually lands.
            await _copyModeGuard.EnsureNotInCopyModeAsync(cancellationToken);

            _logger.LogInformation("Simulating paste with shortcut: {Shortcut} (selection: {Selection})",
                pasteShortcut, usePrimary ? "PRIMARY" : "CLIPBOARD");

            var pasted = await _pasteOrchestrator.StageAndRestoreAsync(
                textToType,
                usePrimary,
                async () =>
                {
                    // Small delay to ensure selection is ready before we trigger the paste.
                    await Task.Delay(50, cancellationToken);

                    if (!await SendPasteShortcutAsync(pasteShortcut, cancellationToken))
                        return false;

                    // Wait long enough for the terminal/tmux/TUI chain to read the
                    // selection before we restore it. 100 ms was too short in tmux —
                    // the terminal paste handler had not yet read the PRIMARY by the
                    // time we wrote the original value back, so the user saw the old
                    // content pasted.
                    await Task.Delay(300, cancellationToken);
                    return true;
                },
                cancellationToken);

            if (pasted)
            {
                _logger.LogInformation("✅ Typed {Length} characters into active window", textToType.Length);
            }
            return pasted;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Text typing was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to type text into active window");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task SendKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Attempted to send empty key");
            return;
        }

        if (!AllowedKeys.Contains(key))
        {
            _logger.LogWarning("Key '{Key}' is not in the allowlist, rejecting", key);
            return;
        }

        try
        {
            _logger.LogInformation("Sending key: {Key}", key);
            var result = await _dotoolRunner.SendKeysAsync(new[] { key }, PasteTimeout, cancellationToken);

            if (result.TimedOut)
                _logger.LogError("dotool SendKey timed out after {Timeout}", PasteTimeout);
            else if (!result.Success)
                _logger.LogError("dotool SendKey failed: {Error}", result.Error);
            else
                _logger.LogDebug("Successfully sent key: {Key}", key);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Key send was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key: {Key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task SendKeySequenceAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || keys.Count == 0) return;

        foreach (var key in keys)
        {
            if (!AllowedKeys.Contains(key))
            {
                _logger.LogWarning("Key '{Key}' in sequence is not in the allowlist, rejecting", key);
                return;
            }
        }

        try
        {
            _logger.LogInformation("Sending key sequence: {Count} keys", keys.Count);
            var result = await _dotoolRunner.SendKeysAsync(keys, SequenceTimeout, cancellationToken);

            if (result.TimedOut)
                _logger.LogError("dotool key sequence timed out after {Timeout}", SequenceTimeout);
            else if (!result.Success)
                _logger.LogError("dotool sequence failed: {Error}", result.Error);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key sequence");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> FastPasteAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("FastPaste: empty text");
            return false;
        }

        try
        {
            var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
            var usePrimary = pasteShortcut == "shift+insert";

            // #1050: cancel any tmux copy-mode on the active pane BEFORE we
            // stage the PRIMARY/CLIPBOARD selection. The guard can take up to
            // its internal timeout (~2 s), and if we staged first, a concurrent
            // clipboard owner (CopyQ auto-sync, another app) could overwrite
            // the selection during that window and our paste would emit stale
            // text. Running the guard first keeps the stage → paste interval
            // as short as possible.
            await _copyModeGuard.EnsureNotInCopyModeAsync(cancellationToken);

            if (usePrimary)
                await _clipboardManager.SetPrimarySelectionAsync(text, cancellationToken);
            else
                await _clipboardManager.SetClipboardAsync(text, cancellationToken);

            await Task.Delay(30, cancellationToken);

            if (!await SendPasteShortcutAsync(pasteShortcut, cancellationToken))
                return false;

            _logger.LogInformation("FastPaste: {Length} chars pasted", text.Length);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("FastPaste cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FastPaste failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task PasteFromClipboardAsync(CancellationToken cancellationToken = default)
    {
        var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
        _logger.LogInformation("PasteFromClipboard: sending {Shortcut}", pasteShortcut);

        // #1050: exit any lingering tmux copy-mode on the active pane so the
        // paste below is delivered to the CLI TUI, not consumed by tmux.
        await _copyModeGuard.EnsureNotInCopyModeAsync(cancellationToken);

        // Shift+Insert path (CLI agent TUIs) reads PRIMARY, not CLIPBOARD.
        // The user clicked "Paste from clipboard" intending to paste the
        // current CLIPBOARD content, so mirror CLIPBOARD → PRIMARY for the
        // duration of the paste, then restore PRIMARY.
        if (pasteShortcut == "shift+insert")
        {
            var clipboard = await _clipboardManager.GetClipboardAsync(cancellationToken);
            if (string.IsNullOrEmpty(clipboard))
            {
                _logger.LogWarning("PasteFromClipboard: clipboard is empty, nothing to paste");
                return;
            }

            await _pasteOrchestrator.StageAndRestoreAsync(
                clipboard,
                usePrimary: true,
                async () =>
                {
                    await Task.Delay(50, cancellationToken);
                    await SendKeyAsync(pasteShortcut, cancellationToken);
                    await Task.Delay(300, cancellationToken);
                    return true;
                },
                cancellationToken);
            return;
        }

        await SendKeyAsync(pasteShortcut, cancellationToken);
    }

    private async Task<bool> SendPasteShortcutAsync(string pasteShortcut, CancellationToken cancellationToken)
    {
        var result = await _dotoolRunner.SendKeysAsync(new[] { pasteShortcut }, PasteTimeout, cancellationToken);

        if (result.TimedOut)
        {
            _logger.LogError("dotool paste timed out after {Timeout}", PasteTimeout);
            return false;
        }

        if (!result.Success)
        {
            _logger.LogError("dotool paste failed: {Error}", result.Error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the appropriate paste shortcut based on the active window type:
    /// - CLI apps running in a terminal (Claude Code, OpenCode, Gemini CLI) use Shift+Insert.
    ///   These TUIs hijack Ctrl+Shift+V for their own purposes (Claude Code treats it as
    ///   "paste image"), so we fall back to the traditional X11 paste binding which goes
    ///   to the terminal first and is delivered to the app as bracketed paste.
    /// - Other terminals use Ctrl+Shift+V (standard terminal paste).
    /// - GUI apps use Ctrl+V.
    /// </summary>
    private async Task<string> GetPasteShortcutAsync(CancellationToken cancellationToken)
    {
        var cliApp = await _cliAppDetector.DetectCliAppAsync(cancellationToken);
        if (cliApp != null)
        {
            _logger.LogInformation(
                "Using paste shortcut: shift+insert (CLI app: {AppName} — Ctrl+Shift+V would be hijacked)",
                cliApp.AppName);
            return EnsureAllowedShortcut("shift+insert");
        }

        var isTerminal = await _terminalDetector.IsTerminalActiveAsync(cancellationToken);
        var pasteShortcut = isTerminal ? "ctrl+shift+v" : "ctrl+v";

        _logger.LogInformation("Using paste shortcut: {Shortcut} (terminal: {IsTerminal})",
            pasteShortcut, isTerminal);

        return EnsureAllowedShortcut(pasteShortcut);
    }

    private static string EnsureAllowedShortcut(string shortcut)
    {
        if (!AllowedPasteShortcuts.Contains(shortcut))
        {
            throw new InvalidOperationException(
                $"Paste shortcut '{shortcut}' is not in the allowed set. " +
                "Refusing to pass an unvetted value to dotool.");
        }
        return shortcut;
    }
}
