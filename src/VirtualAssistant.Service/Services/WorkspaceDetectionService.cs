using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Detects GNOME workspaces using wmctrl to optimize TTS notifications.
/// Skips notifications when user is on the same workspace as the agent terminal.
/// </summary>
public class WorkspaceDetectionService : IWorkspaceDetectionService
{
    private readonly ILogger<WorkspaceDetectionService> _logger;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    // Window title patterns for agent detection
    private static readonly Dictionary<string, string[]> AgentWindowPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["opencode"] = ["opencode", "openai"],
        ["claude"] = ["claude", "anthropic"],
        ["virtualassistant"] = ["virtualassistant", "virtual-assistant"]
    };

    public WorkspaceDetectionService(ILogger<WorkspaceDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<int> GetCurrentWorkspaceAsync(CancellationToken ct = default)
    {
        try
        {
            // wmctrl -d shows workspaces, current has '*'
            var output = await RunCommandAsync("wmctrl", "-d", ct);
            if (string.IsNullOrEmpty(output))
            {
                return -1;
            }

            // Parse: "0  * DG: 2560x1440  VP: 0,0  WA: ..."
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains('*'))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[0], out var workspace))
                    {
                        return workspace;
                    }
                }
            }

            return -1;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get current workspace");
            return -1;
        }
    }

    public async Task<int> GetAgentWorkspaceAsync(string agentName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return -1;
        }

        try
        {
            // wmctrl -l lists windows: "0x00e00004  0 debian Window Title"
            var output = await RunCommandAsync("wmctrl", "-l", ct);
            if (string.IsNullOrEmpty(output))
            {
                return -1;
            }

            // Get patterns for this agent
            var patterns = AgentWindowPatterns.TryGetValue(agentName, out var p)
                ? p
                : [agentName.ToLowerInvariant()];

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var lineLower = line.ToLowerInvariant();

                // Check if any pattern matches
                if (patterns.Any(pattern => lineLower.Contains(pattern)))
                {
                    // Parse workspace number (second column)
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var workspace))
                    {
                        // Skip sticky windows (workspace -1)
                        if (workspace >= 0)
                        {
                            _logger.LogDebug(
                                "Found {Agent} window on workspace {Workspace}: {Title}",
                                agentName, workspace, line);
                            return workspace;
                        }
                    }
                }
            }

            _logger.LogDebug("No window found for agent {Agent}", agentName);
            return -1;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get workspace for agent {Agent}", agentName);
            return -1;
        }
    }

    public async Task<bool> IsUserOnAgentWorkspaceAsync(string agentName, CancellationToken ct = default)
    {
        var currentWorkspace = await GetCurrentWorkspaceAsync(ct);
        if (currentWorkspace < 0)
        {
            // Detection failed → always notify (safe fallback)
            return false;
        }

        var agentWorkspace = await GetAgentWorkspaceAsync(agentName, ct);
        if (agentWorkspace < 0)
        {
            // Agent window not found → always notify (safe fallback)
            return false;
        }

        var sameWorkspace = currentWorkspace == agentWorkspace;

        _logger.LogDebug(
            "Workspace check: user={User}, agent={Agent}({AgentWs}) → {Result}",
            currentWorkspace, agentName, agentWorkspace,
            sameWorkspace ? "same (skip TTS)" : "different (notify)");

        return sameWorkspace;
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
}
