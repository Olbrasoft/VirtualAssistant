using System.Text.Json;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Dtos;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Parses JSON output from Claude Code headless mode.
/// </summary>
public class ClaudeOutputParser : IClaudeOutputParser
{
    private readonly ILogger<ClaudeOutputParser> _logger;

    public ClaudeOutputParser(ILogger<ClaudeOutputParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ClaudeExecutionResult Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return ClaudeExecutionResult.Failed("Empty response from Claude");
        }

        try
        {
            // Claude outputs multiple JSON lines, we want the final result
            var trimmedLines = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Reverse();

            ClaudeJsonResponse? response = null;

            // Parse each line and look for the "result" type
            foreach (var trimmed in trimmedLines)
            {
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
}
