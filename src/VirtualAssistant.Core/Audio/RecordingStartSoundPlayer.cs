using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Service for playing a short sound when dictation recording starts.
/// Provides audio feedback to indicate that voice capture has begun.
/// Uses pw-cat (PipeWire) or paplay (PulseAudio) to play audio.
/// </summary>
public class RecordingStartSoundPlayer : ISoundEffectPlayer, IDisposable
{
    private readonly ILogger<RecordingStartSoundPlayer> _logger;
    private readonly string? _soundFilePath;
    private readonly string? _audioSink;
    private Process? _playProcess;
    private readonly object _lock = new();
    private bool _disposed;
    private string? _cachedPlayer;

    public RecordingStartSoundPlayer(ILogger<RecordingStartSoundPlayer> logger, string? soundFilePath = null, string? audioSink = null)
    {
        _logger = logger;
        _soundFilePath = soundFilePath;
        _audioSink = audioSink;

        ValidateSoundFile(_soundFilePath, "Recording start sound");

        if (!string.IsNullOrWhiteSpace(_audioSink))
        {
            _logger.LogInformation("Audio sink configured: {AudioSink}", _audioSink);
        }
    }

    /// <summary>
    /// Initializes a new instance using sounds directory relative to application base.
    /// </summary>
    public static RecordingStartSoundPlayer CreateFromDirectory(
        ILogger<RecordingStartSoundPlayer> logger,
        string soundsDirectory,
        string recordingStartSoundFileName = "recording-start.mp3",
        string? audioSink = null)
    {
        var soundPath = Path.Combine(soundsDirectory, recordingStartSoundFileName);
        return new RecordingStartSoundPlayer(logger, soundPath, audioSink);
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
    /// Plays the recording start sound once (fire and forget).
    /// </summary>
    public void Play()
    {
        if (_disposed || !IsEnabled)
        {
            _logger.LogDebug("Recording start sound skipped (disposed={Disposed}, enabled={Enabled})", _disposed, IsEnabled);
            return;
        }

        _logger.LogInformation("Playing recording start sound: {Path}", _soundFilePath);

        // Fire and forget - don't wait for completion
        _ = Task.Run(async () =>
        {
            try
            {
                await PlayOnceAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error playing recording start sound");
            }
        });
    }

    /// <summary>
    /// Not applicable for recording start sound - this is a one-shot sound effect.
    /// </summary>
    public void StartLoop()
    {
        // Recording start sound is not designed for looping
        _logger.LogDebug("StartLoop called on RecordingStartSoundPlayer (no-op)");
    }

    /// <summary>
    /// Not applicable for recording start sound - this is a one-shot sound effect.
    /// </summary>
    public void StopLoop()
    {
        // Recording start sound is not designed for looping
        _logger.LogDebug("StopLoop called on RecordingStartSoundPlayer (no-op)");
    }

    private async Task PlayOnceAsync(CancellationToken cancellationToken)
    {
        var player = await GetAvailablePlayerAsync();

        if (string.IsNullOrEmpty(player))
        {
            _logger.LogWarning("No audio player available (tried pw-cat, paplay)");
            return;
        }

        var arguments = GetPlayerArguments(_soundFilePath!);
        _logger.LogDebug("Starting audio player: {Player} {Arguments}", player, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = player,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process? process;
        lock (_lock)
        {
            if (_disposed)
            {
                _logger.LogDebug("Sound player disposed, skipping playback");
                return;
            }

            process = Process.Start(startInfo);
            _playProcess = process;
        }

        if (process == null)
        {
            _logger.LogWarning("Failed to start audio player process: {Player}", player);
            return;
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("Audio player exited with code {ExitCode}: {Stderr}", exitCode, stderr);
            }
            else
            {
                _logger.LogDebug("Recording start sound played successfully");
            }
        }
        finally
        {
            lock (_lock)
            {
                process.Dispose();
                if (_playProcess == process)
                    _playProcess = null;
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
    /// Releases resources used by the recording start sound player.
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
