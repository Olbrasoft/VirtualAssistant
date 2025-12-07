using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Detects GNOME workspaces using the window-calls extension via D-Bus.
/// Works on Wayland (unlike wmctrl which is X11-only).
/// Skips notifications when user is on the same workspace as the agent terminal.
/// </summary>
public class WorkspaceDetectionService : IWorkspaceDetectionService
{
    private readonly ILogger<WorkspaceDetectionService> _logger;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    // Agent window matching patterns: [wm_class patterns], [title patterns]
    private static readonly Dictionary<string, AgentWindowMatcher> AgentMatchers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["opencode"] = new(["kitty"], ["voicevibing", "opencode"]),
        ["claude"] = new(["terminator", "kitty"], ["claude"]),
        ["virtualassistant"] = new(["terminator", "kitty"], ["virtualassistant", "virtual-assistant"])
    };

    public WorkspaceDetectionService(ILogger<WorkspaceDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<int> GetCurrentWorkspaceAsync(CancellationToken ct = default)
    {
        // Not directly applicable with window-calls - we use in_current_workspace instead
        // Return 0 as a placeholder; actual logic is in IsUserOnAgentWorkspaceAsync
        return 0;
    }

    public async Task<int> GetAgentWorkspaceAsync(string agentName, CancellationToken ct = default)
    {
        // Not directly applicable with window-calls - we use in_current_workspace instead
        // Return 0 as a placeholder; actual logic is in IsUserOnAgentWorkspaceAsync
        return 0;
    }

    public async Task<bool> IsUserOnAgentWorkspaceAsync(string agentName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return false;
        }

        try
        {
            var windows = await GetWindowListAsync(ct);
            if (windows == null || windows.Count == 0)
            {
                _logger.LogDebug("No windows found or window-calls extension unavailable");
                return false;
            }

            // Get matcher for this agent
            var matcher = AgentMatchers.TryGetValue(agentName, out var m)
                ? m
                : new AgentWindowMatcher([], [agentName.ToLowerInvariant()]);

            // Find agent window
            foreach (var window in windows)
            {
                if (MatchesAgent(window, matcher))
                {
                    var isOnCurrentWorkspace = window.InCurrentWorkspace;

                    _logger.LogDebug(
                        "Found {Agent} window: title={Title}, class={Class}, inCurrentWs={InCurrent}, focus={Focus}",
                        agentName, window.Title, window.WmClass, isOnCurrentWorkspace, window.Focus);

                    if (isOnCurrentWorkspace)
                    {
                        _logger.LogInformation(
                            "Agent {Agent} is on current workspace - skipping TTS notification",
                            agentName);
                        return true;
                    }

                    return false;
                }
            }

            _logger.LogDebug("No window found for agent {Agent}", agentName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to detect workspace for agent {Agent}", agentName);
            return false;
        }
    }

    private static bool MatchesAgent(WindowInfo window, AgentWindowMatcher matcher)
    {
        var wmClassLower = window.WmClass?.ToLowerInvariant() ?? "";
        var titleLower = window.Title?.ToLowerInvariant() ?? "";

        // Check wm_class match (if patterns specified)
        var classMatches = matcher.WmClassPatterns.Count == 0 ||
            matcher.WmClassPatterns.Any(p => wmClassLower.Contains(p));

        // Check title match
        var titleMatches = matcher.TitlePatterns.Any(p => titleLower.Contains(p));

        return classMatches && titleMatches;
    }

    private async Task<List<WindowInfo>?> GetWindowListAsync(CancellationToken ct)
    {
        // Call window-calls extension via gdbus
        var output = await RunCommandAsync(
            "gdbus",
            "call --session --dest org.gnome.Shell --object-path /org/gnome/Shell/Extensions/Windows --method org.gnome.Shell.Extensions.Windows.List",
            ct);

        if (string.IsNullOrEmpty(output))
        {
            return null;
        }

        // Output format: ('[json array]',)
        // Extract JSON from the tuple format
        var start = output.IndexOf('[');
        var end = output.LastIndexOf(']');

        if (start < 0 || end < 0 || end <= start)
        {
            _logger.LogDebug("Failed to parse window-calls output: {Output}", output);
            return null;
        }

        var json = output.Substring(start, end - start + 1);

        try
        {
            return JsonSerializer.Deserialize<List<WindowInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to deserialize window list");
            return null;
        }
    }

    private static async Task<string> RunCommandAsync(string command, string arguments, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CommandTimeout);

        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            return output;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            return string.Empty;
        }
    }

    private record AgentWindowMatcher(List<string> WmClassPatterns, List<string> TitlePatterns);

    private class WindowInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("in_current_workspace")]
        public bool InCurrentWorkspace { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("wm_class")]
        public string? WmClass { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("wm_class_instance")]
        public string? WmClassInstance { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string? Title { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pid")]
        public int Pid { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("focus")]
        public bool Focus { get; set; }
    }
}
