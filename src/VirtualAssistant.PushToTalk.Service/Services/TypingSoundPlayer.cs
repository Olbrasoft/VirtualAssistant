using System.Diagnostics;

namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

/// <summary>
/// Service for playing typing sound during transcription.
/// Uses pw-play (PipeWire) or paplay (PulseAudio) to play audio.
/// </summary>
public class TypingSoundPlayer : IDisposable
{
    private readonly ILogger<TypingSoundPlayer> _logger;
    private readonly string _soundFilePath;
    private Process? _playProcess;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private readonly object _lock = new();
    private bool _isPlaying;
    private bool _disposed;

    public TypingSoundPlayer(ILogger<TypingSoundPlayer> logger)
    {
        _logger = logger;
        
        // Find the sound file relative to the application
        var baseDir = AppContext.BaseDirectory;
        _soundFilePath = Path.Combine(baseDir, "sounds", "write.mp3");
        
        if (!File.Exists(_soundFilePath))
        {
            _logger.LogWarning("Typing sound file not found: {Path}", _soundFilePath);
        }
        else
        {
            _logger.LogInformation("Typing sound file found: {Path}", _soundFilePath);
        }
    }

    /// <summary>
    /// Starts playing the typing sound in a loop.
    /// </summary>
    public void StartLoop()
    {
        lock (_lock)
        {
            if (_isPlaying || _disposed)
                return;

            if (!File.Exists(_soundFilePath))
            {
                _logger.LogWarning("Cannot play typing sound - file not found");
                return;
            }

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
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async Task PlayOnceAsync(CancellationToken cancellationToken)
    {
        // Try pw-play first (PipeWire), fall back to paplay (PulseAudio)
        var player = await GetAvailablePlayerAsync();
        
        if (string.IsNullOrEmpty(player))
        {
            _logger.LogWarning("No audio player available (tried pw-play, paplay)");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = player,
            Arguments = $"\"{_soundFilePath}\"",
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
        // Check for pw-play (PipeWire)
        if (await IsCommandAvailableAsync("pw-play"))
            return "pw-play";
            
        // Check for paplay (PulseAudio)
        if (await IsCommandAvailableAsync("paplay"))
            return "paplay";
            
        return null;
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
