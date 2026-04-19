using Olbrasoft.LinuxDesktop.Core.Models;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationFocusRouter : IDictationFocusRouter
{
    // WmClass string GNOME reports for the official GNOME Text Editor
    // (the user's dictation scratchpad). The focus-change signal uses
    // "org.gnome.TextEditor.desktop"; window-calls `List` strips the
    // ".desktop" suffix and returns "org.gnome.TextEditor" in WmClass.
    private const string GnomeTextEditorWmClass = "org.gnome.TextEditor";

    // Empirical delay: below ~150 ms the keyboard paste occasionally
    // keystrokes into the outgoing window because GNOME hasn't activated
    // the target yet. 200 ms keeps the hot path snappy while still
    // winning the race reliably on this hardware.
    private static readonly TimeSpan FocusSettleDelay = TimeSpan.FromMilliseconds(200);

    private readonly IWindowQueryService _windowQuery;
    private readonly IWindowActionService _windowAction;
    private readonly ILogger<DictationFocusRouter> _logger;

    public DictationFocusRouter(
        IWindowQueryService windowQuery,
        IWindowActionService windowAction,
        ILogger<DictationFocusRouter> logger)
    {
        _windowQuery = windowQuery ?? throw new ArgumentNullException(nameof(windowQuery));
        _windowAction = windowAction ?? throw new ArgumentNullException(nameof(windowAction));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> TryFocusClaudeCodeIfApplicableAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<WindowInfo> windows;
        try
        {
            windows = await _windowQuery.GetWindowsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Never swallow cancellation — the pipeline's finally block needs
            // to unwind state-machine + feedback even if we get clipped here.
            throw;
        }
        catch (Exception ex)
        {
            // D-Bus / extension failure is non-fatal — we simply skip the
            // focus-switch heuristic and let the caller paste where it is.
            _logger.LogDebug(ex, "Focus router: window enumeration failed, skipping");
            return false;
        }

        var currentWorkspace = windows.Where(w => w.InCurrentWorkspace).ToList();
        var focused = currentWorkspace.FirstOrDefault(w => w.HasFocus);
        if (focused is null)
        {
            _logger.LogDebug("Focus router: no focused window on current workspace, skipping");
            return false;
        }

        if (IsGnomeTextEditor(focused))
        {
            _logger.LogDebug("Focus router: focused window is Text Editor scratchpad, skipping");
            return false;
        }

        if (IsClaudeCode(focused))
        {
            // Already on the right target — nothing to do.
            return false;
        }

        var claudeWindows = currentWorkspace.Where(IsClaudeCode).ToList();
        if (claudeWindows.Count != 1)
        {
            // 0 → nothing to route to; 2+ → ambiguous, leave focus alone.
            _logger.LogDebug(
                "Focus router: {Count} Claude Code windows on current workspace, skipping",
                claudeWindows.Count);
            return false;
        }

        var target = claudeWindows[0];
        _logger.LogInformation(
            "Focus router: activating Claude Code '{Title}' (id {Id}) from '{FromClass}'",
            target.Title, target.Id, focused.WmClass);

        try
        {
            await _windowAction.ActivateWindowAsync(target.Id, cancellationToken);
            await Task.Delay(FocusSettleDelay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Activation failures (D-Bus error, window closed mid-call, etc.)
            // mirror the enumeration failure path: log + fall through to paste
            // against the currently-focused window. Quick Dictation must
            // never be killed by a flaky window-calls extension.
            _logger.LogWarning(
                ex,
                "Focus router: failed to activate Claude Code window {Id}, skipping focus switch",
                target.Id);
            return false;
        }
    }

    private static bool IsClaudeCode(WindowInfo w) =>
        string.Equals(
            CliAgentRegistry.FindByTitle(w.Title)?.AppName,
            "Claude Code",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsGnomeTextEditor(WindowInfo w) =>
        string.Equals(w.WmClass, GnomeTextEditorWmClass, StringComparison.OrdinalIgnoreCase);
}
