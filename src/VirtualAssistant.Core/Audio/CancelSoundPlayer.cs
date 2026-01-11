using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

public class CancelSoundPlayer : SoundPlayerBase
{
    protected override string SoundDescription => "Cancel sound";

    public CancelSoundPlayer(ILogger<CancelSoundPlayer> logger, string? soundFilePath = null, string? audioSink = null)
        : base(logger, soundFilePath, audioSink)
    {
    }

    public static CancelSoundPlayer CreateFromDirectory(
        ILogger<CancelSoundPlayer> logger,
        string soundsDirectory,
        string cancelSoundFileName = "paper-rip.mp3",
        string? audioSink = null)
    {
        var cancelPath = Path.Combine(soundsDirectory, cancelSoundFileName);
        return new CancelSoundPlayer(logger, cancelPath, audioSink);
    }

    public override void Play() => PlayFireAndForget();
}
