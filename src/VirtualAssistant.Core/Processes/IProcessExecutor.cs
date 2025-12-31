using System.Diagnostics;

namespace Olbrasoft.VirtualAssistant.Core.Processes;

/// <summary>
/// Abstraction for executing system processes.
/// Enables dependency injection and testing of process-based operations.
/// </summary>
/// <remarks>
/// This interface follows the Dependency Inversion Principle (DIP) by providing
/// an abstraction layer over System.Diagnostics.Process, making code more testable
/// and allowing process execution to be mocked or replaced in tests.
/// </remarks>
public interface IProcessExecutor
{
    /// <summary>
    /// Starts a process with the specified start information.
    /// </summary>
    /// <param name="startInfo">Process configuration including executable path, arguments, and environment.</param>
    /// <returns>The started process instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when startInfo is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the process fails to start.</exception>
    Process Start(ProcessStartInfo startInfo);

    /// <summary>
    /// Executes a process asynchronously and waits for its completion.
    /// </summary>
    /// <param name="startInfo">Process configuration including executable path, arguments, and environment.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task containing the process execution result with exit code and output.</returns>
    /// <exception cref="ArgumentNullException">Thrown when startInfo is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<ProcessExecutionResult> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a command is available on the system using the Unix <c>which</c> command.
    /// </summary>
    /// <remarks>
    /// This method is intended for Unix-like systems (Linux, macOS) and uses the <c>which</c> command
    /// to check command availability. It will not work on Windows without additional setup.
    /// The VirtualAssistant project targets Debian 13 Linux exclusively.
    /// </remarks>
    /// <param name="command">The command name to check (e.g., "claude", "pw-cat").</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the command is available according to <c>which</c>, false otherwise.</returns>
    Task<bool> IsCommandAvailableAsync(string command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a process execution.
/// </summary>
/// <param name="ExitCode">The process exit code (0 typically means success).</param>
/// <param name="StandardOutput">The captured standard output stream.</param>
/// <param name="StandardError">The captured standard error stream.</param>
public record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// Gets whether the process completed successfully (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;
}
