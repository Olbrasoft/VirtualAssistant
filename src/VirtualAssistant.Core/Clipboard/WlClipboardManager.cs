using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Clipboard;

/// <summary>
/// Clipboard manager implementation for Wayland using wl-clipboard utilities.
/// Requires wl-paste and wl-copy to be installed on the system.
/// </summary>
public class WlClipboardManager : IClipboardManager
{
    private readonly ILogger<WlClipboardManager> _logger;

    public WlClipboardManager(ILogger<WlClipboardManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string?> GetClipboardAsync(CancellationToken cancellationToken = default)
        => ReadSelectionAsync(primary: false, cancellationToken);

    public Task<string?> GetPrimarySelectionAsync(CancellationToken cancellationToken = default)
        => ReadSelectionAsync(primary: true, cancellationToken);

    private async Task<string?> ReadSelectionAsync(bool primary, CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wl-paste",
                    Arguments = primary ? "--primary --no-newline" : "--no-newline",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var content = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                _logger.LogDebug("Retrieved {Selection} content ({Length} chars)",
                    primary ? "PRIMARY" : "clipboard", content?.Length ?? 0);
                return content;
            }

            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogDebug("wl-paste {Selection} failed with exit code {ExitCode}: {Error}",
                primary ? "--primary" : "", process.ExitCode, error);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug("Could not get selection (invalid state): {Message}", ex.Message);
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogDebug("Could not get selection (process error): {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Sets the clipboard content using wl-copy (Wayland clipboard utility).
    /// </summary>
    /// <param name="content">The text content to set in the clipboard.</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    /// <returns>A task that completes when the clipboard is set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when content is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when wl-copy process fails or cannot start.</exception>
    public Task SetClipboardAsync(string content, CancellationToken cancellationToken = default)
        => WriteSelectionAsync(content, primary: false, cancellationToken);

    public Task SetPrimarySelectionAsync(string content, CancellationToken cancellationToken = default)
        => WriteSelectionAsync(content, primary: true, cancellationToken);

    private async Task WriteSelectionAsync(string content, bool primary, CancellationToken cancellationToken)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wl-copy",
                    Arguments = primary ? "--primary" : "",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.StandardInput.WriteAsync(content);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("wl-copy {Selection} failed with exit code {ExitCode}: {Error}",
                    primary ? "--primary" : "", process.ExitCode, error);
                throw new InvalidOperationException($"wl-copy failed: {error}");
            }

            _logger.LogDebug("Set {Selection} content ({Length} chars)",
                primary ? "PRIMARY" : "clipboard", content.Length);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to start wl-copy process");
            throw new InvalidOperationException("Failed to start wl-copy process", ex);
        }
    }
}
