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

    public async Task<string?> GetClipboardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wl-paste",
                    Arguments = "--no-newline",
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
                _logger.LogDebug("Retrieved clipboard content ({Length} chars)", content?.Length ?? 0);
                return content;
            }

            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogDebug("wl-paste failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug("Could not get clipboard (invalid state): {Message}", ex.Message);
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogDebug("Could not get clipboard (process error): {Message}", ex.Message);
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
    public async Task SetClipboardAsync(string content, CancellationToken cancellationToken = default)
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
                _logger.LogError("wl-copy failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                throw new InvalidOperationException($"wl-copy failed: {error}");
            }

            _logger.LogDebug("Set clipboard content ({Length} chars)", content.Length);
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
