using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Abstract base class for sound effect players providing common audio playback functionality.
/// Supports PipeWire (pw-cat) and PulseAudio (paplay) backends with automatic detection.
/// Derived classes implement specific sound behaviors (one-shot, looping).
/// </summary>
public abstract class SoundPlayerBase : ISoundEffectPlayer, IDisposable
{
    protected readonly ILogger Logger;
    protected readonly string? SoundFilePath;
    protected readonly string? AudioSink;
    private Process? _playProcess;
    private readonly object _lock = new();
    protected bool Disposed;
    private string? _cachedPlayer;

    protected SoundPlayerBase(ILogger logger, string? soundFilePath, string? audioSink)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        SoundFilePath = soundFilePath;
        AudioSink = audioSink;

        ValidateSoundFile();
        LogAudioSinkConfiguration();
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(SoundFilePath) && File.Exists(SoundFilePath);

    protected abstract string SoundDescription { get; }

    public abstract void Play();

    public virtual void StartLoop()
    {
        Logger.LogDebug("StartLoop called on {Type} (no-op - one-shot sound)", GetType().Name);
    }

    public virtual void StopLoop()
    {
        Logger.LogDebug("StopLoop called on {Type} (no-op - one-shot sound)", GetType().Name);
    }

    protected void PlayFireAndForget()
    {
        if (Disposed || !IsEnabled)
            return;

        Logger.LogDebug("Playing {SoundDescription}", SoundDescription);

        _ = Task.Run(async () =>
        {
            try
            {
                await PlayOnceAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error playing {SoundDescription}", SoundDescription);
            }
        });
    }

    protected async Task PlayOnceAsync(CancellationToken cancellationToken)
    {
        var player = await GetAvailablePlayerAsync();
        if (string.IsNullOrEmpty(player))
        {
            Logger.LogWarning("No audio player available (tried pw-cat, paplay)");
            return;
        }

        var arguments = GetPlayerArguments(SoundFilePath!);
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
            if (Disposed)
                return;

            process = Process.Start(startInfo);
            _playProcess = process;
        }

        if (process == null)
        {
            Logger.LogWarning("Failed to start audio player process: {Player}", player);
            return;
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken);
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
        if (_cachedPlayer != null)
            return _cachedPlayer;

        if (await IsCommandAvailableAsync("pw-cat"))
        {
            _cachedPlayer = "pw-cat";
            return _cachedPlayer;
        }

        if (await IsCommandAvailableAsync("paplay"))
        {
            _cachedPlayer = "paplay";
            return _cachedPlayer;
        }

        return null;
    }

    private string GetPlayerArguments(string soundPath)
    {
        if (_cachedPlayer == "pw-cat")
        {
            if (!string.IsNullOrWhiteSpace(AudioSink))
                return $"-p --target \"{AudioSink}\" \"{soundPath}\"";
            return $"-p \"{soundPath}\"";
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(AudioSink))
                return $"--device=\"{AudioSink}\" \"{soundPath}\"";
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
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking command availability '{command}': {ex.Message}");
        }

        return false;
    }

    private void ValidateSoundFile()
    {
        if (string.IsNullOrWhiteSpace(SoundFilePath))
            Logger.LogDebug("{SoundDescription} disabled (no path configured)", SoundDescription);
        else if (!File.Exists(SoundFilePath))
            Logger.LogWarning("{SoundDescription} file not found: {Path}", SoundDescription, SoundFilePath);
        else
            Logger.LogInformation("{SoundDescription} file: {Path}", SoundDescription, SoundFilePath);
    }

    private void LogAudioSinkConfiguration()
    {
        if (!string.IsNullOrWhiteSpace(AudioSink))
            Logger.LogInformation("Audio sink configured: {AudioSink}", AudioSink);
    }

    protected void StopPlayProcess()
    {
        lock (_lock)
        {
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
                Logger.LogDebug(ex, "Error stopping play process during dispose");
            }
        }
    }

    public virtual void Dispose()
    {
        if (Disposed)
            return;

        Disposed = true;
        StopPlayProcess();

        GC.SuppressFinalize(this);
    }
}
