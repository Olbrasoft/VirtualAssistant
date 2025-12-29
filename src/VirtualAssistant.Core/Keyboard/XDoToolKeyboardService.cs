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
    private readonly ILogger<XDoToolKeyboardService> _logger;

    public XDoToolKeyboardService(
        IClipboardManager clipboardManager,
        ITerminalDetector terminalDetector,
        ILogger<XDoToolKeyboardService> logger)
    {
        _clipboardManager = clipboardManager ?? throw new ArgumentNullException(nameof(clipboardManager));
        _terminalDetector = terminalDetector ?? throw new ArgumentNullException(nameof(terminalDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Types text into the active window using clipboard + dotool paste.
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

            // Step 1: Save current clipboard content
            var originalClipboard = await _clipboardManager.GetClipboardAsync(cancellationToken);

            // Step 2: Copy our text to clipboard
            await _clipboardManager.SetClipboardAsync(textToType, cancellationToken);

            // Small delay to ensure clipboard is ready
            await Task.Delay(50, cancellationToken);

            // Step 3: Simulate paste using dotool (clipboard already contains our text)
            // Note: dotool type doesn't support Czech diacritics properly, so we use paste simulation
            var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
            _logger.LogInformation("Simulating paste with shortcut: {Shortcut}", pasteShortcut);

            var dotoolProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"echo 'key {pasteShortcut}' | dotool\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dotoolProcess.Start();

            // Add timeout to prevent hanging (max 5 seconds for paste)
            var dotoolTask = dotoolProcess.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            var completedTask = await Task.WhenAny(dotoolTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogError("dotool paste timeout after 5 seconds, killing process");
                try
                {
                    dotoolProcess.Kill();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to kill dotool process");
                }
                return false;
            }

            if (dotoolProcess.ExitCode != 0)
            {
                var error = await dotoolProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("dotool failed with exit code {ExitCode}: {Error}", dotoolProcess.ExitCode, error);
                return false;
            }

            // Small delay to ensure paste completed
            await Task.Delay(100, cancellationToken);

            // Step 4: Restore original clipboard content
            if (!string.IsNullOrEmpty(originalClipboard))
            {
                try
                {
                    await _clipboardManager.SetClipboardAsync(originalClipboard, cancellationToken);
                    _logger.LogDebug("Restored original clipboard content");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Could not restore clipboard: {Message}", ex.Message);
                }
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

    /// <summary>
    /// Gets the appropriate paste shortcut based on the active window type.
    /// Terminals use Ctrl+Shift+V, other applications use Ctrl+V.
    /// </summary>
    private async Task<string> GetPasteShortcutAsync(CancellationToken cancellationToken)
    {
        var isTerminal = await _terminalDetector.IsTerminalActiveAsync(cancellationToken);
        var pasteShortcut = isTerminal ? "ctrl+shift+v" : "ctrl+v";

        _logger.LogInformation("Using paste shortcut: {Shortcut} (terminal: {IsTerminal})",
            pasteShortcut, isTerminal);

        return pasteShortcut;
    }
}
