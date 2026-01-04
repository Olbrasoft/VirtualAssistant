using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Processes;
using Olbrasoft.VirtualAssistant.Service.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProcessExecutor"/>.
/// Verifies process execution, error handling, and command availability checking.
/// </summary>
public class ProcessExecutorTests
{
    private readonly Mock<ILogger<ProcessExecutor>> _loggerMock;
    private readonly ProcessExecutor _executor;

    public ProcessExecutorTests()
    {
        _loggerMock = new Mock<ILogger<ProcessExecutor>>();
        _executor = new ProcessExecutor(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new ProcessExecutor(null!));
        Assert.Equal("logger", exception.ParamName);
    }

    #endregion

    #region Start Tests

    [Fact]
    public void Start_WithNullStartInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => _executor.Start(null!));
        Assert.Equal("startInfo", exception.ParamName);
    }

    [Fact]
    public void Start_WithValidStartInfo_ReturnsProcess()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "echo",
            Arguments = "test",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = _executor.Start(startInfo);

        // Assert
        Assert.NotNull(process);
        Assert.True(process.Id > 0, "Process should have a valid ID");
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithNullStartInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _executor.ExecuteAsync(null!));
        Assert.Equal("startInfo", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulCommand_ReturnsSuccessResult()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "echo",
            Arguments = "Hello World"
        };

        // Act
        var result = await _executor.ExecuteAsync(startInfo);

        // Assert
        Assert.True(result.Success, "Command should succeed");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello World", result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailingCommand_ReturnsFailureResult()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "false" // Command that always returns exit code 1
        };

        // Act
        var result = await _executor.ExecuteAsync(startInfo);

        // Assert
        Assert.False(result.Success, "Command should fail");
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_CancelsExecution()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sleep",
            Arguments = "10" // Long-running command
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        // TaskCanceledException is a subclass of OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _executor.ExecuteAsync(startInfo, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WithArgumentList_ExecutesCorrectly()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "echo"
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add("argument");

        // Act
        var result = await _executor.ExecuteAsync(startInfo);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("test", result.StandardOutput);
        Assert.Contains("argument", result.StandardOutput);
    }

    #endregion

    #region IsCommandAvailableAsync Tests

    [Fact]
    public async Task IsCommandAvailableAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _executor.IsCommandAvailableAsync(null!));
    }

    [Fact]
    public async Task IsCommandAvailableAsync_WithEmptyCommand_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _executor.IsCommandAvailableAsync(string.Empty));
    }

    [Fact]
    public async Task IsCommandAvailableAsync_WithExistingCommand_ReturnsTrue()
    {
        // Act
        var result = await _executor.IsCommandAvailableAsync("echo");

        // Assert
        Assert.True(result, "Expected 'echo' command to be available");
    }

    [Fact]
    public async Task IsCommandAvailableAsync_WithNonExistingCommand_ReturnsFalse()
    {
        // Act
        var result = await _executor.IsCommandAvailableAsync("nonexistent-command-12345");

        // Assert
        Assert.False(result, "Non-existing command should return false");
    }

    [Fact]
    public async Task IsCommandAvailableAsync_WithAlreadyCancelledToken_ReturnsFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        // When token is already cancelled, the exception is caught in the implementation
        // and the method returns false gracefully
        var result = await _executor.IsCommandAvailableAsync("sleep", cts.Token);

        // Assert
        Assert.False(result, "Should return false when token is cancelled");
    }

    #endregion

    #region ProcessExecutionResult Tests

    [Fact]
    public void ProcessExecutionResult_WithExitCodeZero_SuccessIsTrue()
    {
        // Arrange & Act
        var result = new ProcessExecutionResult(0, "output", "");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void ProcessExecutionResult_WithNonZeroExitCode_SuccessIsFalse()
    {
        // Arrange & Act
        var result = new ProcessExecutionResult(1, "", "error");

        // Assert
        Assert.False(result.Success);
    }

    #endregion
}
