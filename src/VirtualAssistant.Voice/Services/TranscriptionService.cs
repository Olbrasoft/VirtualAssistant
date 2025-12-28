using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for transcribing audio using SpeechToText gRPC microservice.
/// Wrapper that delegates to ISpeechTranscriber (SpeechToTextGrpcClient).
/// After transcription, applies text filtering and optionally Mistral LLM correction.
/// Pipeline: Whisper → Text Filtering → Mistral LLM
/// </summary>
public class TranscriptionService : ITranscriptionService
{
    private readonly ILogger<TranscriptionService> _logger;
    private readonly ISpeechTranscriber _transcriber;
    private readonly ITextFilter? _textFilter;
    private readonly ILlmProvider? _llmProvider;
    private readonly ContinuousListenerOptions _options;
    private bool _disposed;

    public TranscriptionService(
        ILogger<TranscriptionService> logger,
        ISpeechTranscriber transcriber,
        IConfiguration configuration,
        ITextFilter? textFilter = null,
        ILlmProvider? llmProvider = null)
    {
        _logger = logger;
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        _textFilter = textFilter; // Optional text filtering (Phase 3)
        _llmProvider = llmProvider; // Optional Mistral LLM for ASR correction (Phase 2)
        _options = new ContinuousListenerOptions();
        configuration.GetSection(ContinuousListenerOptions.SectionName).Bind(_options);
    }

    /// <summary>
    /// Initializes transcriber (no-op for gRPC client, kept for backwards compatibility).
    /// </summary>
    public void Initialize()
    {
        _logger.LogInformation("Transcription service initialized (using gRPC microservice)");
    }

    /// <summary>
    /// Transcribes audio data using SpeechToText gRPC microservice.
    /// If audio is too large, it will be truncated to meet service limits.
    /// After transcription, optionally applies Mistral LLM correction for Czech ASR output.
    /// </summary>
    /// <param name="audioData">16-bit PCM audio data at 16kHz.</param>
    /// <param name="cancellationToken">Cancellation token to abort transcription.</param>
    /// <returns>Transcription result.</returns>
    public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        // Truncate audio if too large (microservice has 10MB limit)
        var safeAudio = TruncateIfTooLarge(audioData);

        // Delegate to gRPC client (thread-safe, handled by microservice)
        var result = await _transcriber.TranscribeAsync(safeAudio, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            return result;

        var originalText = result.Text;  // Whisper raw output
        var processedText = result.Text;
        string? filteredText = null;

        // 1. Apply text filtering (database corrections, file replacements, remove patterns, whitespace normalization)
        if (_textFilter != null && _textFilter.IsEnabled)
        {
            var beforeFilter = processedText;
            processedText = _textFilter.Apply(processedText);

            if (beforeFilter != processedText)
            {
                filteredText = processedText;  // Save filtered text
                _logger.LogInformation("Text filtering applied: '{Before}' → '{After}'",
                    beforeFilter, processedText);
            }
        }

        // 2. Apply LLM correction if available (Mistral for Czech ASR)
        if (_llmProvider != null && !string.IsNullOrWhiteSpace(processedText))
        {
            try
            {
                var beforeLlm = processedText;
                processedText = await _llmProvider.CorrectTextAsync(processedText, cancellationToken);

                if (beforeLlm != processedText)
                {
                    _logger.LogInformation("LLM correction applied: '{Before}' → '{After}'",
                        beforeLlm, processedText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM correction failed, using filtered text: {Error}", ex.Message);
                // Continue with filtered text
            }
        }

        // Return new result with processed text if it changed
        if (processedText != originalText)
        {
            return new TranscriptionResult(processedText, result.Confidence)
            {
                OriginalText = originalText,  // Whisper output before processing
                FilteredText = filteredText   // Text after filtering but before LLM (null if no filtering)
            };
        }

        return result;
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

    /// <summary>
    /// Releases resources used by the transcription service, including the Whisper transcriber model.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _transcriber?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
