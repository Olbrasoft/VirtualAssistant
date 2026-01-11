namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Manages single instance lock for VirtualAssistant.
/// Ensures only one instance of the service can run at a time.
/// </summary>
public interface ISingleInstanceLockManager : IDisposable
{
    /// <summary>
    /// Gets the path to the lock file.
    /// </summary>
    string LockFilePath { get; }

    /// <summary>
    /// Attempts to acquire the single instance lock.
    /// </summary>
    /// <returns>True if lock acquired, false if another instance holds the lock.</returns>
    bool TryAcquire();

    /// <summary>
    /// Releases the lock and cleans up the lock file.
    /// </summary>
    void Release();
}
