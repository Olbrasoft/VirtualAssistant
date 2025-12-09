using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Service for dispatching tasks to Claude Code via headless mode.
/// Executes claude -p command and parses JSON output.
/// </summary>
public class ClaudeDispatchService : IClaudeDispatchService
{
    private readonly ILogger<ClaudeDispatchService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClaudeDispatchOptions _options;

    public ClaudeDispatchService(
        ILogger<ClaudeDispatchService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ClaudeDispatchOptions> options)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<ClaudeExecutionResult> ExecuteAsync(string prompt, string? workingDirectory = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var dir = workingDirectory ?? _options.GetExpandedWorkingDirectory();
        var timeout = TimeSpan.FromMinutes(_options.TimeoutMinutes);

        _logger.LogInformation(
            "Executing Claude headless mode in {Directory}: {Prompt}",
            dir, prompt.Length > 100 ? prompt[..100] + "..." : prompt);

        // Create timeout cancellation
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        Process? process = null;
        try
        {
            // Use ArgumentList instead of Arguments to prevent shell injection
            // ArgumentList handles all escaping automatically and safely
            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(prompt);  // Safe - no manual escaping needed
            startInfo.ArgumentList.Add("--output-format");
            startInfo.ArgumentList.Add("json");

            process = new Process { StartInfo = startInfo };

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

            await process.WaitForExitAsync(linkedCts.Token);

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            _logger.LogDebug("Claude exit code: {Code}, output length: {Len}", process.ExitCode, output.Length);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "Claude execution failed with exit code {Code}: {Error}",
                    process.ExitCode, error);
                await NotifyErrorAsync($"Claude selhal s kódem {process.ExitCode}");
                return ClaudeExecutionResult.Failed(error, process.ExitCode);
            }

            // Parse JSON output
            var result = ParseClaudeOutput(output);

            if (!result.Success)
            {
                await NotifyErrorAsync($"Claude chyba: {result.Error}");
            }

            return result;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogError("Claude execution timed out after {Timeout}", timeout);

            // Kill the process on timeout
            KillProcess(process);

            await NotifyErrorAsync($"Claude timeout po {timeout.TotalMinutes} minutách");
            return ClaudeExecutionResult.Failed($"Timeout after {timeout.TotalMinutes} minutes", -1);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Claude execution was cancelled");
            KillProcess(process);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Claude command");
            await NotifyErrorAsync($"Claude selhání: {ex.Message}");
            return ClaudeExecutionResult.Failed(ex.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Kills the process and its children.
    /// </summary>
    private void KillProcess(Process? process)
    {
        if (process == null || process.HasExited)
            return;

        try
        {
            _logger.LogWarning("Killing Claude process {Pid}", process.Id);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill Claude process");
        }
    }

    /// <summary>
    /// Sends TTS notification for errors.
    /// </summary>
    private async Task NotifyErrorAsync(string message)
    {
        await NotifyAsync(message);
    }

    /// <summary>
    /// Sends TTS notification for success.
    /// </summary>
    public async Task NotifySuccessAsync(string message)
    {
        if (_options.NotifyOnSuccess)
        {
            await NotifyAsync(message);
        }
    }

    /// <summary>
    /// Sends TTS notification.
    /// </summary>
    private async Task NotifyAsync(string message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(
                JsonSerializer.Serialize(new { text = message, source = "claude" }),
                Encoding.UTF8,
                "application/json");

            await client.PostAsync(_options.TtsNotifyUrl, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send TTS notification");
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
