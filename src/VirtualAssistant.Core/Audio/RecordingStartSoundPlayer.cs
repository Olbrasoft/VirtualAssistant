using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Audio;

public class RecordingStartSoundPlayer : SoundPlayerBase
{
    protected override string SoundDescription => "Recording start sound";

    public RecordingStartSoundPlayer(ILogger<RecordingStartSoundPlayer> logger, string? soundFilePath = null, string? audioSink = null)
        : base(logger, soundFilePath, audioSink)
    {
    }

    public static RecordingStartSoundPlayer CreateFromDirectory(
        ILogger<RecordingStartSoundPlayer> logger,
        string soundsDirectory,
        string recordingStartSoundFileName = "recording-start.mp3",
        string? audioSink = null)
    {
        var soundPath = Path.Combine(soundsDirectory, recordingStartSoundFileName);
        return new RecordingStartSoundPlayer(logger, soundPath, audioSink);
    }

    public override void Play()
    {
        if (Disposed || !IsEnabled)
        {
            Logger.LogDebug("Recording start sound skipped (disposed={Disposed}, enabled={Enabled})", Disposed, IsEnabled);
            return;
        }

        Logger.LogInformation("Playing recording start sound: {Path}", SoundFilePath);
        PlayFireAndForget();
    }
}
