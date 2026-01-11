using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Sound player for typing/keystroke feedback sounds with loop support.
/// Unlike one-shot sound players, this player can continuously loop the sound
/// (e.g., during dictation or typing simulation).
/// </summary>
public class TypingSoundPlayer : SoundPlayerBase
{
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private readonly object _loopLock = new();
    private bool _isPlaying;

    protected override string SoundDescription => "Typing sound";

    public TypingSoundPlayer(
        ILogger<TypingSoundPlayer> logger,
        string? soundFilePath = null,
        string? audioSink = null)
        : base(logger, soundFilePath, audioSink)
    {
    }

    public static TypingSoundPlayer CreateFromDirectory(
        ILogger<TypingSoundPlayer> logger,
        string soundsDirectory,
        string typingSoundFileName = "write.mp3",
        string? audioSink = null)
    {
        var typingPath = Path.Combine(soundsDirectory, typingSoundFileName);
        return new TypingSoundPlayer(logger, typingPath, audioSink);
    }

    public override void Play()
    {
        if (Disposed || !IsEnabled)
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
                Logger.LogDebug(ex, "Error playing typing sound");
            }
        });
    }

    public override void StartLoop()
    {
        lock (_loopLock)
        {
            if (_isPlaying || Disposed || !IsEnabled)
                return;

            _isPlaying = true;
            _loopCts = new CancellationTokenSource();
            _loopTask = PlayLoopAsync(_loopCts.Token);

            Logger.LogDebug("Typing sound loop started");
        }
    }

    public override void StopLoop()
    {
        lock (_loopLock)
        {
            if (!_isPlaying)
                return;

            _isPlaying = false;
            _loopCts?.Cancel();

            StopPlayProcess();

            Logger.LogDebug("Typing sound loop stopped");
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
                Logger.LogDebug(ex, "Error in play loop");
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

    public override void Dispose()
    {
        if (Disposed)
            return;

        Disposed = true;
        StopLoop();
        _loopCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}
