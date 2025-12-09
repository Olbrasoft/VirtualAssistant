using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for audio playback control.
/// Handles playing audio files and stopping playback when needed.
/// </summary>
public interface IAudioPlaybackService
{
    /// <summary>
    /// Plays an audio file asynchronously.
    /// Monitors speech lock during playback and stops if lock is acquired.
    /// </summary>
    /// <param name="audioFile">Path to the audio file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PlayAsync(string audioFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops any currently playing audio immediately.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets whether audio is currently playing.
    /// </summary>
    bool IsPlaying { get; }
}

/// <summary>
/// Audio playback service using ffplay.
/// Monitors speech lock during playback and automatically stops if lock is acquired.
/// </summary>
public sealed class AudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private readonly ILogger<AudioPlaybackService> _logger;
    private readonly ISpeechLockService _speechLockService;
    private Process? _currentProcess;
    private readonly object _processLock = new();

    public AudioPlaybackService(
        ILogger<AudioPlaybackService> logger,
        ISpeechLockService speechLockService)
    {
        _logger = logger;
        _speechLockService = speechLockService;
    }

    /// <inheritdoc />
    public bool IsPlaying
    {
        get
        {
            lock (_processLock)
            {
                return _currentProcess != null && !_currentProcess.HasExited;
            }
        }
    }

    /// <inheritdoc />
    public async Task PlayAsync(string audioFile, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/ffplay",
                Arguments = $"-nodisp -autoexit -loglevel quiet \"{audioFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        lock (_processLock)
        {
            _currentProcess = process;
        }

        try
        {
            process.Start();

            // Monitor for speech lock while playing - check every 50ms
            while (!process.HasExited)
            {
                // Check if user started recording (speech lock acquired)
                if (_speechLockService.IsLocked)
                {
                    _logger.LogInformation("🛑 Speech lock detected during playback - stopping");
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Process may have exited already
                    }
                    break;
                }

                // Wait 50ms before next check, or until process exits
                try
                {
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Ignore
                    }
                    throw;
                }
            }

            // Ensure process has exited
            if (!process.HasExited)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        finally
        {
            lock (_processLock)
            {
                _currentProcess = null;
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        try
        {
            Process? process;
            lock (_processLock)
            {
                process = _currentProcess;
            }

            if (process != null && !process.HasExited)
            {
                _logger.LogInformation("🛑 Stopping audio playback");
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping audio playback");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
