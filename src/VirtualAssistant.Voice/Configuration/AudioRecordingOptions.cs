using System.ComponentModel.DataAnnotations;

namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for audio recording.
/// </summary>
public class AudioRecordingOptions
{
    public const string SectionName = "AudioRecording";

    /// <summary>
    /// Sample rate in Hz.
    /// Default: 16000 Hz (16 kHz).
    /// </summary>
    [Range(8000, 48000, ErrorMessage = "SampleRate must be between 8000 and 48000 Hz")]
    public int SampleRate { get; set; } = 16000;

    /// <summary>
    /// Bits per sample.
    /// Default: 16 bits.
    /// </summary>
    [Range(8, 32, ErrorMessage = "BitsPerSample must be between 8 and 32")]
    public int BitsPerSample { get; set; } = 16;

    /// <summary>
    /// Number of audio channels.
    /// Default: 1 (mono).
    /// </summary>
    [Range(1, 2, ErrorMessage = "Channels must be 1 (mono) or 2 (stereo)")]
    public int Channels { get; set; } = 1;

    /// <summary>
    /// Maximum recording duration in minutes.
    /// Default: 16 minutes.
    /// </summary>
    [Range(1, 60, ErrorMessage = "MaxRecordingDurationMinutes must be between 1 and 60")]
    public int MaxRecordingDurationMinutes { get; set; } = 16;

    /// <summary>
    /// Maximum audio buffer size in bytes.
    /// Calculated from: SampleRate * (BitsPerSample / 8) * Channels * MaxRecordingDurationMinutes * 60
    /// Default: ~32 MB (16 minutes at 16kHz 16-bit mono).
    /// </summary>
    public int MaxBufferSizeBytes => SampleRate * (BitsPerSample / 8) * Channels * MaxRecordingDurationMinutes * 60;

    /// <summary>
    /// Bytes per sample (BitsPerSample / 8).
    /// Default: 2 bytes (16-bit).
    /// </summary>
    public int BytesPerSample => BitsPerSample / 8;

    /// <summary>
    /// Milliseconds per second constant.
    /// </summary>
    public const int MillisecondsPerSecond = 1000;
}
