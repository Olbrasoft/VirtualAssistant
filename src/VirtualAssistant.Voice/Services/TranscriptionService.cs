using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for transcribing audio using Whisper.net.
/// Thread-safe wrapper around WhisperNetTranscriber.
/// </summary>
public class TranscriptionService : IDisposable
{
    private readonly ILogger<TranscriptionService> _logger;
    private readonly ContinuousListenerOptions _options;
    // Only ONE transcriber to avoid Whisper.net CUDA/native library conflicts
    // Having two WhisperFactory instances causes SIGSEGV crashes
    private WhisperNetTranscriber? _transcriber;
    private bool _disposed;
    
    // Whisper.net native library is NOT thread-safe - only one transcription at a time
    private readonly SemaphoreSlim _transcriptionLock = new(1, 1);

    public TranscriptionService(ILogger<TranscriptionService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _options = new ContinuousListenerOptions();
        configuration.GetSection(ContinuousListenerOptions.SectionName).Bind(_options);
    }

    /// <summary>
    /// Initializes Whisper transcriber (single model for all transcription).
    /// </summary>
    public void Initialize()
    {
        if (_transcriber != null)
            return;

        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Use single model for everything to avoid SIGSEGV crashes
        // Two WhisperFactory instances sharing CUDA context cause segfaults
        _logger.LogInformation("Loading Whisper model from: {Path}", _options.WhisperModelPath);
        var whisperLogger = loggerFactory.CreateLogger<WhisperNetTranscriber>();
        _transcriber = new WhisperNetTranscriber(whisperLogger, _options.WhisperModelPath, _options.WhisperLanguage);
        _logger.LogInformation("Whisper model loaded (single model for all transcription)");
    }

    /// <summary>
    /// Transcribes audio data using the Whisper model.
    /// If audio is too large, it will be truncated to prevent Whisper.net crashes.
    /// </summary>
    /// <param name="audioData">16-bit PCM audio data at 16kHz.</param>
    /// <returns>Transcription result.</returns>
    public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData)
    {
        if (_transcriber == null)
        {
            throw new InvalidOperationException("Transcriber not initialized. Call Initialize() first.");
        }

        // Truncate audio if too large to prevent SIGSEGV crashes
        var safeAudio = TruncateIfTooLarge(audioData);

        // Whisper.net is NOT thread-safe - acquire lock before transcription
        await _transcriptionLock.WaitAsync();
        try
        {
            var result = await _transcriber.TranscribeAsync(safeAudio);
            return result;
        }
        finally
        {
            _transcriptionLock.Release();
        }
    }

    /// <summary>
    /// Truncates audio data if it exceeds the maximum segment size.
    /// Takes the last MaxSegmentBytes to preserve the most recent speech.
    /// </summary>
    private byte[] TruncateIfTooLarge(byte[] audioData)
    {
        if (audioData.Length <= _options.MaxSegmentBytes)
        {
            return audioData;
        }

        _logger.LogWarning("Audio too large ({Size} bytes > {Max} bytes), truncating to last {Max} bytes", 
            audioData.Length, _options.MaxSegmentBytes, _options.MaxSegmentBytes);

        // Take the last MaxSegmentBytes (most recent audio)
        var truncated = new byte[_options.MaxSegmentBytes];
        Buffer.BlockCopy(audioData, audioData.Length - _options.MaxSegmentBytes, truncated, 0, _options.MaxSegmentBytes);
        return truncated;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _transcriber?.Dispose();
        _transcriber = null;
        _transcriptionLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
