using Olbrasoft.VirtualAssistant.Service.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Services;

public class SingleInstanceLockManagerTests : IDisposable
{
    private readonly string _testLockFilePath;
    private readonly List<SingleInstanceLockManager> _managersToDispose = [];

    public SingleInstanceLockManagerTests()
    {
        _testLockFilePath = Path.Combine(Path.GetTempPath(), $"test-lock-{Guid.NewGuid()}.lock");
    }

    public void Dispose()
    {
        foreach (var manager in _managersToDispose)
        {
            manager.Dispose();
        }

        if (File.Exists(_testLockFilePath))
        {
            try { File.Delete(_testLockFilePath); } catch { }
        }
    }

    private SingleInstanceLockManager CreateManager(string? path = null)
    {
        var manager = new SingleInstanceLockManager(path ?? _testLockFilePath);
        _managersToDispose.Add(manager);
        return manager;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLockFilePath_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SingleInstanceLockManager(null!));
        Assert.Equal("lockFilePath", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyLockFilePath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SingleInstanceLockManager(string.Empty));
        Assert.Equal("lockFilePath", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithWhitespaceLockFilePath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SingleInstanceLockManager("   "));
        Assert.Equal("lockFilePath", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidPath_SetsLockFilePath()
    {
        var manager = CreateManager();

        Assert.Equal(_testLockFilePath, manager.LockFilePath);
    }

    #endregion

    #region TryAcquire Tests

    [Fact]
    public void TryAcquire_WhenLockNotHeld_ReturnsTrue()
    {
        var manager = CreateManager();

        var result = manager.TryAcquire();

        Assert.True(result);
    }

    [Fact]
    public void TryAcquire_WhenLockNotHeld_CreatesLockFile()
    {
        var manager = CreateManager();

        manager.TryAcquire();

        Assert.True(File.Exists(_testLockFilePath));
    }

    [Fact]
    public void TryAcquire_WhenLockNotHeld_CreatesLockFileWithContent()
    {
        var manager = CreateManager();

        manager.TryAcquire();

        Assert.True(File.Exists(_testLockFilePath));
        var fileInfo = new FileInfo(_testLockFilePath);
        Assert.True(fileInfo.Length > 0, "Lock file should contain PID");
    }

    [Fact]
    public void TryAcquire_WhenAlreadyAcquired_ReturnsTrue()
    {
        var manager = CreateManager();
        manager.TryAcquire();

        var result = manager.TryAcquire();

        Assert.True(result);
    }

    [Fact]
    public void TryAcquire_WhenAnotherInstanceHoldsLock_ReturnsFalse()
    {
        var manager1 = CreateManager();
        var manager2 = CreateManager();
        manager1.TryAcquire();

        var result = manager2.TryAcquire();

        Assert.False(result);
    }

    #endregion

    #region Release Tests

    [Fact]
    public void Release_WhenLockHeld_DeletesLockFile()
    {
        var manager = CreateManager();
        manager.TryAcquire();

        manager.Release();

        Assert.False(File.Exists(_testLockFilePath));
    }

    [Fact]
    public void Release_WhenLockNotHeld_DoesNotThrow()
    {
        var manager = CreateManager();

        var exception = Record.Exception(() => manager.Release());

        Assert.Null(exception);
    }

    [Fact]
    public void Release_AllowsAnotherInstanceToAcquire()
    {
        var manager1 = CreateManager();
        var manager2 = CreateManager();
        manager1.TryAcquire();
        manager1.Release();

        var result = manager2.TryAcquire();

        Assert.True(result);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ReleasesLock()
    {
        var lockPath = Path.Combine(Path.GetTempPath(), $"dispose-test-{Guid.NewGuid()}.lock");
        var manager1 = new SingleInstanceLockManager(lockPath);
        manager1.TryAcquire();
        manager1.Dispose();

        var manager2 = new SingleInstanceLockManager(lockPath);
        var result = manager2.TryAcquire();
        manager2.Dispose();

        Assert.True(result);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var manager = CreateManager();
        manager.TryAcquire();

        var exception = Record.Exception(() =>
        {
            manager.Dispose();
            manager.Dispose();
            manager.Dispose();
        });

        Assert.Null(exception);
    }

    #endregion
}
