using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Service for playing cancel sound when transcription is cancelled.
/// Plays a paper-rip sound effect to indicate discarding of transcribed content.
/// Uses pw-cat (PipeWire) or paplay (PulseAudio) to play audio.
/// </summary>
public class CancelSoundPlayer : IDisposable
{
    private readonly ILogger<CancelSoundPlayer> _logger;
    private readonly string? _soundFilePath;
    private readonly string? _audioSink;
    private Process? _playProcess;
    private readonly object _lock = new();
    private bool _disposed;
    private string? _cachedPlayer;

    public CancelSoundPlayer(ILogger<CancelSoundPlayer> logger, string? soundFilePath = null, string? audioSink = null)
    {
        _logger = logger;
        _soundFilePath = soundFilePath;
        _audioSink = audioSink;

        ValidateSoundFile(_soundFilePath, "Cancel sound");

        if (!string.IsNullOrWhiteSpace(_audioSink))
        {
            _logger.LogInformation("Audio sink configured: {AudioSink}", _audioSink);
        }
    }

    /// <summary>
    /// Initializes a new instance using sounds directory relative to application base.
    /// </summary>
    public static CancelSoundPlayer CreateFromDirectory(
        ILogger<CancelSoundPlayer> logger,
        string soundsDirectory,
        string cancelSoundFileName = "paper-rip.mp3",
        string? audioSink = null)
    {
        var cancelPath = Path.Combine(soundsDirectory, cancelSoundFileName);
        return new CancelSoundPlayer(logger, cancelPath, audioSink);
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
    /// Plays the cancel sound once (fire and forget).
    /// </summary>
    public void Play()
    {
        if (_disposed || !IsEnabled)
            return;

        _logger.LogDebug("Playing cancel sound");

        // Fire and forget - don't wait for completion
        _ = Task.Run(async () =>
        {
            try
            {
                await PlayOnceAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error playing cancel sound");
            }
        });
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
            if (_disposed)
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
    /// Releases resources used by the cancel sound player.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop any running play process
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
            _logger.LogDebug(ex, "Error stopping play process during dispose");
        }

        GC.SuppressFinalize(this);
    }
}
