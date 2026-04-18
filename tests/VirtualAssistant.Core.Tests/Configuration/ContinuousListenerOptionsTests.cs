using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Configuration;

/// <summary>
/// Pins the three computed byte-math properties and the two path-resolution
/// branches of GetFullWhisperModelPath that don't require filesystem access.
/// The filename-only branch delegates to WhisperModelLocator which walks
/// real XDG directories and is covered separately.
/// </summary>
public class ContinuousListenerOptionsTests
{
    [Fact]
    public void ChunkSizeBytes_UsesSampleRateAndVadChunkMs_At16BitsPerSample()
    {
        // 16000 Hz * 32 ms / 1000 * 2 bytes = 1024 bytes per VAD chunk.
        var options = new ContinuousListenerOptions { SampleRate = 16000, VadChunkMs = 32 };

        Assert.Equal(1024, options.ChunkSizeBytes);
    }

    [Fact]
    public void ChunkSizeBytes_ReflectsCustomSampleRate()
    {
        // 48000 Hz * 10 ms / 1000 * 2 bytes = 960 bytes per VAD chunk.
        var options = new ContinuousListenerOptions { SampleRate = 48000, VadChunkMs = 10 };

        Assert.Equal(960, options.ChunkSizeBytes);
    }

    [Fact]
    public void PreBufferMaxBytes_ScalesWithSampleRateAndPreBufferMs()
    {
        // 16000 * 1000 / 1000 * 2 = 32000 bytes for a one-second pre-buffer at 16kHz.
        var options = new ContinuousListenerOptions { SampleRate = 16000, PreBufferMs = 1000 };

        Assert.Equal(32000, options.PreBufferMaxBytes);
    }

    [Fact]
    public void MaxSegmentBytes_UsesLongMath_ToAvoidInt32Overflow()
    {
        // 96kHz * 600s would overflow int32 at the SampleRate*MaxSegmentMs
        // multiplication step. The computed property promotes to long, so we
        // should get 96000 * 600000 / 1000 * 2 = 115,200,000 bytes.
        var options = new ContinuousListenerOptions { SampleRate = 96000, MaxSegmentMs = 600000 };

        Assert.Equal(115_200_000, options.MaxSegmentBytes);
    }

    [Fact]
    public void GetFullWhisperModelPath_AbsolutePath_IsReturnedVerbatim()
    {
        var abs = Path.Combine(Path.GetTempPath(), "some-model.bin");
        var options = new ContinuousListenerOptions { WhisperModelPath = abs };

        Assert.Equal(abs, options.GetFullWhisperModelPath());
    }

    [Fact]
    public void GetFullWhisperModelPath_RelativeWithSeparator_IsJoinedToBaseDirectory()
    {
        // Contains a path separator → falls through the filename-only shortcut
        // and is combined with AppContext.BaseDirectory.
        var options = new ContinuousListenerOptions { WhisperModelPath = "models/foo.bin" };

        var expected = Path.Combine(AppContext.BaseDirectory, "models/foo.bin");
        Assert.Equal(expected, options.GetFullWhisperModelPath());
    }

    [Fact]
    public void Defaults_AreReasonableForContinuousListening()
    {
        var options = new ContinuousListenerOptions();

        Assert.Equal(16000, options.SampleRate);
        Assert.Equal(32, options.VadChunkMs);
        Assert.Equal(1000, options.PreBufferMs);
        Assert.Equal(1500, options.PostSilenceMs);
        Assert.Equal(800, options.MinRecordingMs);
        Assert.Equal("cs", options.WhisperLanguage);
        Assert.True(options.UseGpu);
        Assert.Equal(60000, options.MaxSegmentMs);
        Assert.Equal(5053, options.LogViewerPort);
        Assert.False(options.StartMuted);
        Assert.Equal(string.Empty, options.SileroVadModelPath);
        Assert.Equal(string.Empty, options.WhisperModelPath);
    }
}
