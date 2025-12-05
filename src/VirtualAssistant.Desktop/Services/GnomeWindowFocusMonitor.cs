using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Desktop.Events;
using VirtualAssistant.Desktop.Models;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// GNOME-specific window focus monitor using D-Bus polling via window-calls extension.
/// </summary>
public class GnomeWindowFocusMonitor : IWindowFocusMonitor, IDisposable
{
    private readonly ILogger<GnomeWindowFocusMonitor> _logger;
    private readonly TimeSpan _pollingInterval;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private bool _disposed;

    public WindowInfo? CurrentWindow { get; private set; }
    public WindowInfo? PreviousWindow { get; private set; }

    public event EventHandler<WindowFocusChangedEventArgs>? FocusChanged;

    public GnomeWindowFocusMonitor(ILogger<GnomeWindowFocusMonitor> logger, TimeSpan? pollingInterval = null)
    {
        _logger = logger;
        _pollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_pollingTask != null)
        {
            _logger.LogWarning("Window focus monitor is already running");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = PollWindowFocusAsync(_cts.Token);

        _logger.LogInformation("Window focus monitor started with {Interval}ms polling interval", 
            _pollingInterval.TotalMilliseconds);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts == null || _pollingTask == null)
        {
            return;
        }

        _logger.LogInformation("Stopping window focus monitor");

        await _cts.CancelAsync();

        try
        {
            await _pollingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cts.Dispose();
        _cts = null;
        _pollingTask = null;
    }

    private async Task PollWindowFocusAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var focusedWindow = await GetFocusedWindowAsync(cancellationToken);

                if (focusedWindow != null && (CurrentWindow == null || CurrentWindow.Id != focusedWindow.Id))
                {
                    var previousWindow = CurrentWindow;
                    PreviousWindow = previousWindow;
                    CurrentWindow = focusedWindow;

                    _logger.LogInformation("Focus changed: {Previous} ({PreviousId}) -> {Current} ({CurrentId})",
                        previousWindow?.WmClass ?? "none",
                        previousWindow?.Id.ToString() ?? "N/A",
                        focusedWindow.WmClass,
                        focusedWindow.Id);

                    FocusChanged?.Invoke(this, new WindowFocusChangedEventArgs(previousWindow, focusedWindow));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling window focus");
            }

            try
            {
                await Task.Delay(_pollingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task<WindowInfo?> GetFocusedWindowAsync(CancellationToken cancellationToken)
    {
        var json = await CallWindowCallsListAsync(cancellationToken);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return ParseFocusedWindow(json);
    }

    internal async Task<string?> CallWindowCallsListAsync(CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gdbus",
                Arguments = "call --session --dest org.gnome.Shell --object-path /org/gnome/Shell/Extensions/Windows --method org.gnome.Shell.Extensions.Windows.List",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogWarning("gdbus call failed: {Error}", error);
            return null;
        }

        return output;
    }

    internal WindowInfo? ParseFocusedWindow(string gdbusOutput)
    {
        try
        {
            // gdbus output format: ('[ {...}, {...} ]',)
            // We need to extract the JSON array from inside the parentheses and quotes
            var startIndex = gdbusOutput.IndexOf('[');
            var endIndex = gdbusOutput.LastIndexOf(']');

            if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
            {
                _logger.LogWarning("Invalid gdbus output format: {Output}", gdbusOutput);
                return null;
            }

            var json = gdbusOutput.Substring(startIndex, endIndex - startIndex + 1);

            var windows = JsonSerializer.Deserialize<List<WindowCallsWindow>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var focused = windows?.FirstOrDefault(w => w.Focus);
            if (focused == null)
            {
                return null;
            }

            return new WindowInfo(
                focused.Id,
                focused.Wm_class ?? "unknown",
                focused.Title ?? "unknown",
                DateTime.Now
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse window list JSON");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal class for deserializing window-calls JSON response.
    /// </summary>
    internal class WindowCallsWindow
    {
        public uint Id { get; set; }
        public string? Wm_class { get; set; }
        public string? Title { get; set; }
        public bool Focus { get; set; }
    }
}
