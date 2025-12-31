using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Service for playing typing sound during transcription.
/// Uses pw-cat (PipeWire) or paplay (PulseAudio) to play audio.
/// </summary>
public class TypingSoundPlayer : ISoundEffectPlayer, IDisposable
{
    private readonly ILogger<TypingSoundPlayer> _logger;
    private readonly string? _soundFilePath;
    private readonly string? _audioSink;
    private Process? _playProcess;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private readonly object _lock = new();
    private bool _isPlaying;
    private bool _disposed;
    private string? _cachedPlayer;

    public TypingSoundPlayer(ILogger<TypingSoundPlayer> logger, string? soundFilePath = null, string? audioSink = null)
    {
        _logger = logger;
        _soundFilePath = soundFilePath;
        _audioSink = audioSink;

        ValidateSoundFile(_soundFilePath, "Typing sound");

        if (!string.IsNullOrWhiteSpace(_audioSink))
        {
            _logger.LogInformation("Audio sink configured: {AudioSink}", _audioSink);
        }
    }

    /// <summary>
    /// Initializes a new instance using sounds directory relative to application base.
    /// </summary>
    public static TypingSoundPlayer CreateFromDirectory(
        ILogger<TypingSoundPlayer> logger,
        string soundsDirectory,
        string typingSoundFileName = "write.mp3",
        string? audioSink = null)
    {
        var typingPath = Path.Combine(soundsDirectory, typingSoundFileName);
        return new TypingSoundPlayer(logger, typingPath, audioSink);
    }

    private void ValidateSoundFile(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogDebug("{Description} disabled (no path configured)", description);
        }
        else if (!File.Exists(path))
        {
            _logger.LogWarning("{Description} file not found: {Path}", description, path);
        }
        else
        {
            _logger.LogInformation("{Description} file: {Path}", description, path);
        }
    }

    /// <summary>
    /// Gets whether sound playback is enabled.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_soundFilePath) && File.Exists(_soundFilePath);

    /// <summary>
    /// Plays the typing sound once.
    /// </summary>
    public void Play()
    {
        if (_disposed || !IsEnabled)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await PlayOnceAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error playing typing sound");
            }
        });
    }

    /// <summary>
    /// Starts playing the typing sound in a loop.
    /// </summary>
    public void StartLoop()
    {
        lock (_lock)
        {
            if (_isPlaying || _disposed || !IsEnabled)
                return;

            _isPlaying = true;
            _loopCts = new CancellationTokenSource();
            _loopTask = PlayLoopAsync(_loopCts.Token);

            _logger.LogDebug("Typing sound loop started");
        }
    }

    /// <summary>
    /// Stops the typing sound loop.
    /// </summary>
    public void StopLoop()
    {
        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _isPlaying = false;
            _loopCts?.Cancel();

            // Kill any running play process
            try
            {
                if (_playProcess != null && !_playProcess.HasExited)
                {
                    _playProcess.Kill();
                    _playProcess.Dispose();
                    _playProcess = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error stopping play process");
            }

            _logger.LogDebug("Typing sound loop stopped");
        }
    }

    private async Task PlayLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PlayOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in play loop");
                // Small delay before retry
                try
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task PlayOnceAsync(CancellationToken cancellationToken)
    {
        var player = await GetAvailablePlayerAsync();

        if (string.IsNullOrEmpty(player))
        {
            _logger.LogWarning("No audio player available (tried pw-cat, paplay)");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = player,
            Arguments = GetPlayerArguments(_soundFilePath!),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _playProcess = Process.Start(startInfo);
        }

        if (_playProcess != null)
        {
            try
            {
                await _playProcess.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                lock (_lock)
                {
                    _playProcess?.Dispose();
                    _playProcess = null;
                }
            }
        }
    }

    private async Task<string?> GetAvailablePlayerAsync()
    {
        // Return cached player if already found
        if (_cachedPlayer != null)
            return _cachedPlayer;

        // Check for pw-cat (PipeWire) - requires -p flag for playback mode
        if (await IsCommandAvailableAsync("pw-cat"))
        {
            _cachedPlayer = "pw-cat";
            return _cachedPlayer;
        }

        // Fallback to paplay (PulseAudio) for systems without PipeWire
        if (await IsCommandAvailableAsync("paplay"))
        {
            _cachedPlayer = "paplay";
            return _cachedPlayer;
        }

        return null;
    }

    /// <summary>
    /// Gets the command line arguments for playing a sound file.
    /// </summary>
    private string GetPlayerArguments(string soundPath)
    {
        if (_cachedPlayer == "pw-cat")
        {
            // pw-cat -p --target <sink> <file>
            if (!string.IsNullOrWhiteSpace(_audioSink))
            {
                return $"-p --target \"{_audioSink}\" \"{soundPath}\"";
            }
            return $"-p \"{soundPath}\"";
        }
        else // paplay
        {
            // paplay --device=<sink> <file>
            if (!string.IsNullOrWhiteSpace(_audioSink))
            {
                return $"--device=\"{_audioSink}\" \"{soundPath}\"";
            }
            return $"\"{soundPath}\"";
        }
    }

    private static async Task<bool> IsCommandAvailableAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // Ignore
        }

        return false;
    }

    /// <summary>
    /// Releases resources used by the typing sound player, including stopping any active playback.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopLoop();
        _loopCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}
