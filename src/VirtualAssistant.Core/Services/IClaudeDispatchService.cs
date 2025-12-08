namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for dispatching tasks to Claude Code via headless mode.
/// Executes claude -p command and manages the process lifecycle.
/// </summary>
public interface IClaudeDispatchService
{
    /// <summary>
    /// Execute a task via Claude Code headless mode.
    /// </summary>
    /// <param name="prompt">The task prompt to send to Claude</param>
    /// <param name="workingDirectory">Optional working directory for Claude</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing session_id and execution details</returns>
    Task<ClaudeExecutionResult> ExecuteAsync(string prompt, string? workingDirectory = null, CancellationToken ct = default);

    /// <summary>
    /// Check if Claude Code is installed and available.
    /// </summary>
    /// <returns>True if claude command is available</returns>
    Task<bool> IsClaudeAvailableAsync();
}

/// <summary>
/// Result of Claude Code headless execution.
/// </summary>
public class ClaudeExecutionResult
{
    /// <summary>
    /// Whether the execution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Claude session ID from the execution.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Result content from Claude.
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// Total cost in USD for this execution.
    /// </summary>
    public decimal? TotalCostUsd { get; init; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether the result was an error from Claude.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Process exit code.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ClaudeExecutionResult Succeeded(string? sessionId, string? result, decimal? cost) => new()
    {
        Success = true,
        SessionId = sessionId,
        Result = result,
        TotalCostUsd = cost,
        ExitCode = 0
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ClaudeExecutionResult Failed(string error, int exitCode = 1) => new()
    {
        Success = false,
        Error = error,
        ExitCode = exitCode
    };

    /// <summary>
    /// Creates a result from Claude error response.
    /// </summary>
    public static ClaudeExecutionResult ClaudeError(string? sessionId, string error) => new()
    {
        Success = false,
        IsError = true,
        SessionId = sessionId,
        Error = error,
        ExitCode = 0
    };
}
