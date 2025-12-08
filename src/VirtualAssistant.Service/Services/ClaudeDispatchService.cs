using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Service for dispatching tasks to Claude Code via headless mode.
/// Executes claude -p command and parses JSON output.
/// </summary>
public class ClaudeDispatchService : IClaudeDispatchService
{
    private readonly ILogger<ClaudeDispatchService> _logger;
    private readonly string _defaultWorkingDirectory;

    public ClaudeDispatchService(ILogger<ClaudeDispatchService> logger)
    {
        _logger = logger;
        // Default to VirtualAssistant source directory
        _defaultWorkingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Olbrasoft", "VirtualAssistant");
    }

    public async Task<ClaudeExecutionResult> ExecuteAsync(string prompt, string? workingDirectory = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var dir = workingDirectory ?? _defaultWorkingDirectory;

        _logger.LogInformation(
            "Executing Claude headless mode in {Directory}: {Prompt}",
            dir, prompt.Length > 100 ? prompt[..100] + "..." : prompt);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = $"-p \"{EscapePrompt(prompt)}\" --output-format json",
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            _logger.LogDebug("Claude exit code: {Code}, output length: {Len}", process.ExitCode, output.Length);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "Claude execution failed with exit code {Code}: {Error}",
                    process.ExitCode, error);
                return ClaudeExecutionResult.Failed(error, process.ExitCode);
            }

            // Parse JSON output
            return ParseClaudeOutput(output);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Claude execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Claude command");
            return ClaudeExecutionResult.Failed(ex.Message);
        }
    }

    public async Task<bool> IsClaudeAvailableAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "claude",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private ClaudeExecutionResult ParseClaudeOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return ClaudeExecutionResult.Failed("Empty response from Claude");
        }

        try
        {
            // Claude outputs multiple JSON lines, we want the final result
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ClaudeJsonResponse? response = null;

            // Parse each line and look for the "result" type
            foreach (var line in lines.Reverse())
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith('{'))
                {
                    continue;
                }

                try
                {
                    var parsed = JsonSerializer.Deserialize<ClaudeJsonResponse>(trimmed);
                    if (parsed?.Type == "result")
                    {
                        response = parsed;
                        break;
                    }
                }
                catch (JsonException)
                {
                    // Continue to next line
                }
            }

            if (response == null)
            {
                // Try parsing the whole output as a single JSON
                response = JsonSerializer.Deserialize<ClaudeJsonResponse>(output);
            }

            if (response == null)
            {
                return ClaudeExecutionResult.Failed("Failed to parse Claude JSON output");
            }

            if (response.IsError == true)
            {
                return ClaudeExecutionResult.ClaudeError(response.SessionId, response.Result ?? "Unknown error");
            }

            _logger.LogInformation(
                "Claude execution completed. Session: {Session}, Cost: ${Cost}",
                response.SessionId, response.TotalCostUsd);

            return ClaudeExecutionResult.Succeeded(
                response.SessionId,
                response.Result,
                response.TotalCostUsd);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Claude JSON response: {Output}", output);
            return ClaudeExecutionResult.Failed($"JSON parse error: {ex.Message}");
        }
    }

    private static string EscapePrompt(string prompt)
    {
        // Escape double quotes and backslashes for shell argument
        return prompt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`");
    }

    /// <summary>
    /// JSON response from Claude Code headless mode.
    /// </summary>
    private class ClaudeJsonResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("subtype")]
        public string? Subtype { get; set; }

        [JsonPropertyName("total_cost_usd")]
        public decimal? TotalCostUsd { get; set; }

        [JsonPropertyName("is_error")]
        public bool? IsError { get; set; }

        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("session_id")]
        public string? SessionId { get; set; }
    }
}
