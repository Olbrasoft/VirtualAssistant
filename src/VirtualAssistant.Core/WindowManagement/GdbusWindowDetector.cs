using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <inheritdoc />
public sealed class GdbusWindowDetector : IGdbusWindowDetector
{
    private readonly ILogger<GdbusWindowDetector> _logger;

    public GdbusWindowDetector(ILogger<GdbusWindowDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FocusedWindowInfo?> GetFocusedWindowInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gdbus",
                    Arguments = "call --session --dest org.gnome.Shell " +
                               "--object-path /org/gnome/Shell/Extensions/Windows " +
                               "--method org.gnome.Shell.Extensions.Windows.List",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("D-Bus window-calls returned no data or failed");
                return null;
            }

            var rawJsonArray = GdbusJsonHelper.TryExtractJsonArray(output);
            if (rawJsonArray is null)
            {
                _logger.LogDebug("Could not parse D-Bus output");
                return null;
            }

            var jsonArray = GdbusJsonHelper.UnescapeQuotes(rawJsonArray);
            var windows = JsonSerializer.Deserialize<JsonElement>(jsonArray);

            foreach (var window in windows.EnumerateArray())
            {
                if (window.TryGetProperty("focus", out var focusProp) && focusProp.GetBoolean())
                {
                    var wmClass = window.TryGetProperty("wm_class", out var wmClassProp)
                        ? wmClassProp.GetString() ?? ""
                        : "";
                    var pid = window.TryGetProperty("pid", out var pidProp)
                        ? pidProp.GetInt32()
                        : 0;
                    var title = window.TryGetProperty("title", out var titleProp)
                        ? titleProp.GetString() ?? ""
                        : "";

                    if (pid > 0)
                    {
                        _logger.LogDebug("Focused window: {WmClass} \"{Title}\" (PID: {Pid})", wmClass, title, pid);
                        return new FocusedWindowInfo(wmClass, pid, title);
                    }
                }
            }

            _logger.LogDebug("No focused window found");
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse D-Bus JSON response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get focused window info");
            return null;
        }
    }
}
