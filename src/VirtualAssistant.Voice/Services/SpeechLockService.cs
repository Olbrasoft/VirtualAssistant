using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Speech lock service with in-memory lock and file fallback.
/// Prevents TTS playback while user is recording/speaking.
/// Lock is released ONLY when explicitly unlocked - no timeout.
/// </summary>
public sealed class SpeechLockService : ISpeechLockService, IDisposable
{
    private readonly string _lockFilePath;
    private readonly ILogger<SpeechLockService> _logger;

    private volatile bool _isLocked;
    private readonly object _lock = new();
    private bool _disposed;

    public SpeechLockService(
        ILogger<SpeechLockService> logger,
        IOptions<SystemPathsOptions> options)
    {
        _logger = logger;
        _lockFilePath = options.Value.SpeechLockFile;

        // Clean up stale lock file from previous crashed sessions
        try
        {
            if (File.Exists(_lockFilePath))
            {
                File.Delete(_lockFilePath);
                _logger.LogInformation("Deleted stale speech lock file on startup: {Path}", _lockFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete stale lock file: {Path}", _lockFilePath);
        }

        _logger.LogDebug("SpeechLockService initialized. Lock file: {Path} (no timeout - unlock only on explicit stop)",
            _lockFilePath);
    }

    /// <inheritdoc />
    public bool IsLocked
    {
        get
        {
            // Check both in-memory flag and file-based lock (backward compatibility)
            if (_isLocked) return true;

            try
            {
                return File.Exists(_lockFilePath);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public void Lock(TimeSpan? timeout = null)
    {
        // Note: timeout parameter is ignored - lock is released only on explicit Unlock()
        lock (_lock)
        {
            if (_disposed) return;

            _isLocked = true;
            _logger.LogInformation("Speech locked (no timeout - will unlock only on explicit stop)");
        }
    }

    /// <inheritdoc />
    public void Unlock()
    {
        lock (_lock)
        {
            if (!_isLocked)
            {
                _logger.LogDebug("Unlock called but was not locked");
                return;
            }

            _isLocked = false;
            _logger.LogInformation("Speech unlocked");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
