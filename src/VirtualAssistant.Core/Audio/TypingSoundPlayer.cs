using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Processes;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

public class TypingSoundPlayer : SoundPlayerBase
{
    private readonly IProcessExecutor _processExecutor;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private readonly object _loopLock = new();
    private bool _isPlaying;

    protected override string SoundDescription => "Typing sound";

    public TypingSoundPlayer(
        ILogger<TypingSoundPlayer> logger,
        IProcessExecutor processExecutor,
        string? soundFilePath = null,
        string? audioSink = null)
        : base(logger, soundFilePath, audioSink)
    {
        _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
    }

    public static TypingSoundPlayer CreateFromDirectory(
        ILogger<TypingSoundPlayer> logger,
        IProcessExecutor processExecutor,
        string soundsDirectory,
        string typingSoundFileName = "write.mp3",
        string? audioSink = null)
    {
        var typingPath = Path.Combine(soundsDirectory, typingSoundFileName);
        return new TypingSoundPlayer(logger, processExecutor, typingPath, audioSink);
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
