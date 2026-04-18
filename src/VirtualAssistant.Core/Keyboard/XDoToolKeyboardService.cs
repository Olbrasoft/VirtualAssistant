using System.Diagnostics;
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
    private readonly IClipboardManager _clipboardManager;
    private readonly ITerminalDetector _terminalDetector;
    private readonly ICliAppDetector _cliAppDetector;
    private readonly ILogger<XDoToolKeyboardService> _logger;

    public XDoToolKeyboardService(
        IClipboardManager clipboardManager,
        ITerminalDetector terminalDetector,
        ICliAppDetector cliAppDetector,
        ILogger<XDoToolKeyboardService> logger)
    {
        _clipboardManager = clipboardManager ?? throw new ArgumentNullException(nameof(clipboardManager));
        _terminalDetector = terminalDetector ?? throw new ArgumentNullException(nameof(terminalDetector));
        _cliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Defense-in-depth: paste shortcuts are never passed to a shell, but the
    // value is still used as dotool input. Keep the allowed set explicit so a
    // future refactor that changes the source of this value cannot slip in
    // something unexpected.
    private static readonly HashSet<string> AllowedPasteShortcuts = new(StringComparer.OrdinalIgnoreCase)
    {
        "ctrl+v", "ctrl+shift+v", "shift+insert"
    };

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

            // Add space after text
            var textToType = text + " ";

            var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
            var usePrimary = pasteShortcut == "shift+insert";

            // Step 1: Save current selection we are about to overwrite
            string? originalClipboard = null;
            string? originalPrimary = null;
            if (usePrimary)
            {
                originalPrimary = await _clipboardManager.GetPrimarySelectionAsync(cancellationToken);
                await _clipboardManager.SetPrimarySelectionAsync(textToType, cancellationToken);
            }
            else
            {
                originalClipboard = await _clipboardManager.GetClipboardAsync(cancellationToken);
                await _clipboardManager.SetClipboardAsync(textToType, cancellationToken);
            }

            // Small delay to ensure selection is ready before we trigger the paste
            await Task.Delay(50, cancellationToken);

            _logger.LogInformation("Simulating paste with shortcut: {Shortcut} (selection: {Selection})",
                pasteShortcut, usePrimary ? "PRIMARY" : "CLIPBOARD");

            // Invoke dotool directly and pipe the command over stdin. The previous
            // implementation composed a `bash -c "echo 'key …' | dotool"` string with
            // string interpolation — safe only because pasteShortcut came from a
            // hardcoded set, but a shell-injection foot-gun if that ever changes.
            using var dotoolProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotool",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dotoolProcess.Start();
            await dotoolProcess.StandardInput.WriteLineAsync($"key {pasteShortcut}");
            dotoolProcess.StandardInput.Close();

            // Use an independent timeout CTS. Binding the timer to `cancellationToken`
            // would conflate "user cancelled" with "dotool hung 5 s", and the timeout
            // branch below would log a fake timeout when the real cause was cancellation.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var dotoolTask = dotoolProcess.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token);
            var completedTask = await Task.WhenAny(dotoolTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogError("dotool paste timeout after 5 seconds, killing process");
                TryKillProcess(dotoolProcess);
                return false;
            }

            // Surface cancellation as OperationCanceledException (caught below) rather
            // than as a silent "timeout" false-positive.
            await dotoolTask;

            if (dotoolProcess.ExitCode != 0)
            {
                var error = await dotoolProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("dotool failed with exit code {ExitCode}: {Error}", dotoolProcess.ExitCode, error);
                return false;
            }

            // Wait long enough for the terminal/tmux/TUI chain to read the
            // selection before we restore it. 100 ms was too short in tmux —
            // the terminal paste handler had not yet read the PRIMARY by the
            // time we wrote the original value back, so the user saw the old
            // content pasted.
            await Task.Delay(300, cancellationToken);

            // Step 4: Restore the original selection we overwrote
            try
            {
                if (usePrimary && !string.IsNullOrEmpty(originalPrimary))
                {
                    await _clipboardManager.SetPrimarySelectionAsync(originalPrimary, cancellationToken);
                    _logger.LogDebug("Restored original PRIMARY selection");
                }
                else if (!usePrimary && !string.IsNullOrEmpty(originalClipboard))
                {
                    await _clipboardManager.SetClipboardAsync(originalClipboard, cancellationToken);
                    _logger.LogDebug("Restored original clipboard content");
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Could not restore selection: {Message}", ex.Message);
            }

            _logger.LogInformation("✅ Typed {Length} characters into active window", textToType.Length);
            return true;
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

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "enter", "ctrl+u", "ctrl+v", "ctrl+shift+v", "shift+insert",
        "escape", "tab", "backspace", "delete", "alt+F4",
        "super+kp1", "super+kp2", "super+kp3", "super+kp4",
        "super+kp5", "super+kp6", "super+kp7", "super+kp8",
        "end", "ctrl+a"
    };

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

            using var dotoolProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotool",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dotoolProcess.Start();
            await dotoolProcess.StandardInput.WriteLineAsync($"key {key}");
            dotoolProcess.StandardInput.Close();

            var dotoolTask = dotoolProcess.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            var completedTask = await Task.WhenAny(dotoolTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogError("dotool SendKey timeout after 5 seconds, killing process");
                try { dotoolProcess.Kill(); } catch { /* best effort */ }
                return;
            }

            if (dotoolProcess.ExitCode != 0)
            {
                var error = await dotoolProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("dotool SendKey failed with exit code {ExitCode}: {Error}", dotoolProcess.ExitCode, error);
            }
            else
            {
                _logger.LogDebug("Successfully sent key: {Key}", key);
            }
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

            using var dotoolProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotool",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dotoolProcess.Start();

            foreach (var key in keys)
                await dotoolProcess.StandardInput.WriteLineAsync($"key {key}");

            dotoolProcess.StandardInput.Close();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var dotoolTask = dotoolProcess.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token);

            var completedTask = await Task.WhenAny(dotoolTask, timeoutTask);

            cancellationToken.ThrowIfCancellationRequested();

            if (completedTask != dotoolTask)
            {
                _logger.LogError("dotool key sequence timeout, killing process");
                try { dotoolProcess.Kill(); } catch { /* best effort */ }
                return;
            }

            if (dotoolProcess.ExitCode != 0)
            {
                var error = await dotoolProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("dotool sequence failed: {Error}", error);
            }
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

        Process? dotoolProcess = null;

        try
        {
            // 1. Detect paste shortcut first so we know which selection to write to
            var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
            var usePrimary = pasteShortcut == "shift+insert";

            // 2. Stage the text in the right selection (Shift+Insert reads PRIMARY)
            if (usePrimary)
                await _clipboardManager.SetPrimarySelectionAsync(text, cancellationToken);
            else
                await _clipboardManager.SetClipboardAsync(text, cancellationToken);

            await Task.Delay(30, cancellationToken);

            dotoolProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotool",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dotoolProcess.Start();
            await dotoolProcess.StandardInput.WriteLineAsync($"key {pasteShortcut}");
            dotoolProcess.StandardInput.Close();

            var dotoolTask = dotoolProcess.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var completedTask = await Task.WhenAny(dotoolTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogError("FastPaste: dotool timeout");
                TryKillProcess(dotoolProcess);
                return false;
            }

            await dotoolTask;

            if (dotoolProcess.ExitCode != 0)
            {
                var error = await dotoolProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("FastPaste failed: {Error}", error);
                return false;
            }

            _logger.LogInformation("FastPaste: {Length} chars pasted", text.Length);
            return true;
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(dotoolProcess);
            _logger.LogInformation("FastPaste cancelled");
            return false;
        }
        catch (Exception ex)
        {
            TryKillProcess(dotoolProcess);
            _logger.LogError(ex, "FastPaste failed");
            return false;
        }
        finally
        {
            dotoolProcess?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task PasteFromClipboardAsync(CancellationToken cancellationToken = default)
    {
        var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
        _logger.LogInformation("PasteFromClipboard: sending {Shortcut}", pasteShortcut);

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

            var originalPrimary = await _clipboardManager.GetPrimarySelectionAsync(cancellationToken);
            try
            {
                await _clipboardManager.SetPrimarySelectionAsync(clipboard, cancellationToken);
                await Task.Delay(50, cancellationToken);
                await SendKeyAsync(pasteShortcut, cancellationToken);
                await Task.Delay(300, cancellationToken);
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalPrimary))
                {
                    try { await _clipboardManager.SetPrimarySelectionAsync(originalPrimary, cancellationToken); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Could not restore PRIMARY selection"); }
                }
            }
            return;
        }

        await SendKeyAsync(pasteShortcut, cancellationToken);
    }

    private static void TryKillProcess(Process? process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited) process.Kill();
        }
        catch { /* best effort */ }
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
