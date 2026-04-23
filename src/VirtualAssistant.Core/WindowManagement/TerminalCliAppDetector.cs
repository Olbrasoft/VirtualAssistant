using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Detects CLI applications (Claude Code, OpenCode, Gemini CLI) running inside
/// the currently focused terminal. Responsibilities are limited to the
/// focused-window path:
/// <list type="bullet">
/// <item><see cref="IGdbusWindowDetector"/> — resolves the focused window via GNOME Shell.</item>
/// <item><see cref="CliAppDetectionCache"/> — serves the last successful result during transient gdbus failures.</item>
/// <item><see cref="ITerminalAgentIdentifier"/> — runs the three-way (process-tree / title / tmux) agent identification, shared with the dictation focus router.</item>
/// </list>
/// </summary>
public class TerminalCliAppDetector : ICliAppDetector
{
    private readonly ILogger<TerminalCliAppDetector> _logger;
    private readonly IGdbusWindowDetector _gdbus;
    private readonly ITerminalAgentIdentifier _identifier;
    private readonly CliAppDetectionCache _cache;

    public TerminalCliAppDetector(
        ILogger<TerminalCliAppDetector> logger,
        IGdbusWindowDetector gdbus,
        ITerminalAgentIdentifier identifier,
        CliAppDetectionCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gdbus = gdbus ?? throw new ArgumentNullException(nameof(gdbus));
        _identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<CliAppDetectionResult?> DetectCliAppAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var focusedWindow = await _gdbus.GetFocusedWindowInfoAsync(cancellationToken);
            if (focusedWindow == null)
            {
                return _cache.TryGet("gdbus probe returned no focused window info");
            }

            if (!TerminalWindowClasses.IsTerminal(focusedWindow.Value.WmClass))
            {
                _logger.LogDebug("Focused window is not a terminal: {WmClass}", focusedWindow.Value.WmClass);
                // Confirmed non-terminal focus: invalidate cache so a later gdbus
                // hiccup can't serve the stale CLI-app result.
                _cache.Clear();
                return null;
            }

            _logger.LogDebug("Terminal detected: {WmClass} (PID: {Pid}), checking for CLI apps...",
                focusedWindow.Value.WmClass, focusedWindow.Value.Pid);

            var agent = await _identifier.IdentifyAsync(
                focusedWindow.Value.Title,
                focusedWindow.Value.Pid,
                cancellationToken);

            if (agent is null)
            {
                _logger.LogDebug("No known CLI apps detected in terminal descendants, title or tmux sessions");
                // Confirmed terminal without any known CLI app (e.g. plain bash).
                _cache.Clear();
                return null;
            }

            var result = new CliAppDetectionResult(agent.AppName, agent.PromptFileName);
            _cache.Set(result);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during CLI app detection");
            return _cache.TryGet("unhandled exception during detection");
        }
    }
}
