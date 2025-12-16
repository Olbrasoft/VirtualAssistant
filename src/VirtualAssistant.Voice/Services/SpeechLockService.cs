using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Speech lock service with in-memory lock, file fallback, and auto-timeout.
/// Prevents TTS playback while user is recording/speaking.
/// </summary>
public sealed class SpeechLockService : ISpeechLockService, IDisposable
{
    private readonly string _lockFilePath;
    private readonly TimeSpan _defaultTimeout;
    private readonly ILogger<SpeechLockService> _logger;

    private volatile bool _isLocked;
    private Timer? _timeoutTimer;
    private readonly object _lock = new();
    private bool _disposed;

    public SpeechLockService(
        ILogger<SpeechLockService> logger,
        IOptions<SystemPathsOptions> options)
    {
        _logger = logger;
        _lockFilePath = options.Value.SpeechLockFile;
        _defaultTimeout = TimeSpan.FromSeconds(options.Value.SpeechLockTimeoutSeconds);

        _logger.LogDebug("SpeechLockService initialized. Lock file: {Path}, Default timeout: {Timeout}s",
            _lockFilePath, _defaultTimeout.TotalSeconds);
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
        lock (_lock)
        {
            if (_disposed) return;

            _isLocked = true;

            // Cancel any existing timeout timer
            _timeoutTimer?.Dispose();

            var actualTimeout = timeout ?? _defaultTimeout;

            // Set up auto-unlock timer for safety
            _timeoutTimer = new Timer(
                _ => AutoUnlock(actualTimeout),
                null,
                actualTimeout,
                Timeout.InfiniteTimeSpan);

            _logger.LogInformation("Speech locked (timeout: {Timeout}s)", actualTimeout.TotalSeconds);
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
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;

            _logger.LogInformation("Speech unlocked");
        }
    }

    private void AutoUnlock(TimeSpan timeout)
    {
        lock (_lock)
        {
            if (!_isLocked) return;

            _logger.LogWarning("Speech lock timeout - auto-unlocking after {Timeout}s (STT may have crashed or network error)",
                timeout.TotalSeconds);

            _isLocked = false;
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
        }
    }
}
