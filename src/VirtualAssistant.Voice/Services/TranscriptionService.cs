using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for transcribing audio using SpeechToText gRPC microservice.
/// Wrapper that delegates to ISpeechTranscriber (SpeechToTextGrpcClient).
/// After transcription, applies text filtering and optionally LLM correction.
/// Pipeline: Whisper → Text Filtering → LLM (via factory or racing)
/// </summary>
public class TranscriptionService : ITranscriptionService
{
    private readonly ILogger<TranscriptionService> _logger;
    private readonly ISpeechTranscriber _transcriber;
    private readonly ITextFilter? _textFilter;
    private readonly ILightweightTextFilter? _lightweightTextFilter;
    private readonly ILlmProviderFactory? _llmProviderFactory;
    private readonly IRacingLlmProvider? _racingLlmProvider;
    private readonly RacingOptions _racingOptions;
    private readonly ContinuousListenerOptions _options;
    private bool _disposed;

    /// <inheritdoc/>
    public event Action<string>? RawTranscriptionReady;

    public TranscriptionService(
        ILogger<TranscriptionService> logger,
        ISpeechTranscriber transcriber,
        IConfiguration configuration,
        ITextFilter? textFilter = null,
        ILightweightTextFilter? lightweightTextFilter = null,
        ILlmProviderFactory? llmProviderFactory = null,
        IRacingLlmProvider? racingLlmProvider = null,
        IOptions<LlmProviderOptions>? llmProviderOptions = null)
    {
        _logger = logger;
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        _textFilter = textFilter;
        _lightweightTextFilter = lightweightTextFilter;
        _llmProviderFactory = llmProviderFactory;
        _racingLlmProvider = racingLlmProvider;
        _racingOptions = llmProviderOptions?.Value.Racing ?? new RacingOptions();
        _options = new ContinuousListenerOptions();
        configuration.GetSection(ContinuousListenerOptions.SectionName).Bind(_options);

        _logger.LogInformation("TranscriptionService racing config: Enabled={Enabled}, Providers=[{Providers}], RacingProvider={HasRacing}, ActiveProvider={Active}",
            _racingOptions.Enabled,
            string.Join(", ", _racingOptions.Providers),
            _racingLlmProvider != null,
            llmProviderOptions?.Value.ActiveProvider ?? "null");

        // Also check raw config
        var rawEnabled = configuration["LlmProvider:Racing:Enabled"];
        var rawProviders = configuration["LlmProvider:Racing:Providers"];
        _logger.LogInformation("TranscriptionService racing RAW config: Enabled={RawEnabled}, Providers={RawProviders}",
            rawEnabled ?? "null", rawProviders ?? "null");
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

        // Notify listeners that raw transcription is ready (before LLM correction)
        try
        {
            RawTranscriptionReady?.Invoke(originalText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RawTranscriptionReady handler failed");
        }
        string? filteredText = null;
        int? llmDurationMs = null;
        int? promptId = null;
        int? modelId = null;
        int? inputTokens = null;
        int? outputTokens = null;
        int? reasoningTokens = null;

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

        // 2. Apply LLM correction
        Guid? raceGroupId = null;
        Task<LlmCorrectionResult?>? racingLoserTask = null;
        string? racingLoserProviderName = null;

        if (!string.IsNullOrWhiteSpace(processedText))
        {
            // Racing mode: call multiple providers in parallel, use first response
            if (_racingOptions.Enabled && _racingLlmProvider != null)
            {
                try
                {
                    var racingResult = await _racingLlmProvider.CorrectTextAsync(processedText, cancellationToken);
                    if (racingResult != null)
                    {
                        var beforeLlm = processedText;
                        processedText = racingResult.WinnerResult.CorrectedText;
                        llmDurationMs = racingResult.WinnerResult.DurationMs;
                        promptId = racingResult.WinnerResult.PromptId;
                        modelId = racingResult.WinnerResult.ModelId;
                        inputTokens = racingResult.WinnerResult.InputTokens;
                        outputTokens = racingResult.WinnerResult.OutputTokens;
                        reasoningTokens = racingResult.WinnerResult.ReasoningTokens;

                        // Only set race metadata when a real race occurred (i.e., a loser exists)
                        if (racingResult.LoserTask != null)
                        {
                            raceGroupId = racingResult.RaceGroupId;
                            racingLoserTask = racingResult.LoserTask;
                            racingLoserProviderName = racingResult.LoserProviderName;
                        }

                        if (beforeLlm != processedText)
                        {
                            _logger.LogInformation("LLM racing correction by {Winner} in {Duration}ms: '{Before}' → '{After}'",
                                racingResult.WinnerProviderName, llmDurationMs, beforeLlm, processedText);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LLM racing correction failed, using filtered text: {Error}", ex.Message);
                }
            }
            // Single provider mode: call active provider only
            else if (_llmProviderFactory != null)
            {
                try
                {
                    var llmProvider = _llmProviderFactory.GetActiveProvider();
                    var beforeLlm = processedText;
                    var correctionResult = await llmProvider.CorrectTextAsync(processedText, cancellationToken);
                    processedText = correctionResult.CorrectedText;
                    llmDurationMs = correctionResult.DurationMs;
                    promptId = correctionResult.PromptId;
                    modelId = correctionResult.ModelId;
                    inputTokens = correctionResult.InputTokens;
                    outputTokens = correctionResult.OutputTokens;
                    reasoningTokens = correctionResult.ReasoningTokens;

                    if (beforeLlm != processedText)
                    {
                        _logger.LogInformation("LLM correction applied in {Duration}ms: '{Before}' → '{After}'",
                            llmDurationMs, beforeLlm, processedText);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LLM correction failed, using filtered text: {Error}", ex.Message);
                }
            }
        }

        // Return new result with processed text if it changed
        if (processedText != originalText)
        {
            return new TranscriptionResult(processedText, result.Confidence)
            {
                OriginalText = originalText,
                FilteredText = filteredText,
                LlmDurationMs = llmDurationMs,
                PromptId = promptId,
                ModelId = modelId,
                SttProviderId = result.SttProviderId,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                ReasoningTokens = reasoningTokens,
                RaceGroupId = raceGroupId,
                RacingLoserTask = racingLoserTask,
                RacingLoserProviderName = racingLoserProviderName
            };
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<TranscriptionResult> TranscribeRawAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        var safeAudio = TruncateIfTooLarge(audioData);
        var result = await _transcriber.TranscribeAsync(safeAudio, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            return result;
        }

        var rawText = result.Text;

        // Fire RawTranscriptionReady with the UNFILTERED Whisper output. The event name
        // promises raw STT and listeners (e.g. transcription persistence, UI raw view)
        // expect to see exactly what Whisper produced — they will perform their own
        // filtering as needed. The filtering below only affects the returned
        // TranscriptionResult, not the event payload.
        try
        {
            RawTranscriptionReady?.Invoke(rawText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RawTranscriptionReady handler failed");
        }

        // Apply lightweight filter (Whisper hallucination removal + whitespace normalization).
        // Skips DB corrections and LLM correction to preserve Quick Dictation latency budget.
        // The full pipeline is intentionally NOT used here.
        var filteredText = _lightweightTextFilter?.Apply(rawText) ?? rawText;

        if (string.IsNullOrWhiteSpace(filteredText))
        {
            // Whole-text matched a hallucination pattern → wipe the result so the caller
            // (e.g. DictationWorker) skips paste/Enter via its IsNullOrWhiteSpace check.
            _logger.LogInformation(
                "TranscribeRawAsync: text wiped by lightweight filter (hallucination): '{Original}'",
                rawText);
            return new TranscriptionResult(string.Empty, result.Confidence)
            {
                OriginalText = rawText,
                FilteredText = string.Empty,
                SttProviderId = result.SttProviderId
            };
        }

        if (ReferenceEquals(filteredText, rawText))
        {
            return result;
        }

        return new TranscriptionResult(filteredText, result.Confidence)
        {
            OriginalText = rawText,
            FilteredText = filteredText,
            SttProviderId = result.SttProviderId
        };
    }

    /// <inheritdoc/>
    public Task<TranscriptionResult> FinalizePreTranscribedRawAsync(string rawText, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return Task.FromResult(new TranscriptionResult("Empty pre-transcribed text"));
        }

        try
        {
            RawTranscriptionReady?.Invoke(rawText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RawTranscriptionReady handler failed (streaming finalize)");
        }

        var filteredText = _lightweightTextFilter?.Apply(rawText) ?? rawText;

        // Confidence 0.0f is a sentinel for "unknown" — this path has no per-chunk
        // confidence data to aggregate, so we don't claim high confidence like a
        // single Whisper pass would return.
        if (string.IsNullOrWhiteSpace(filteredText))
        {
            _logger.LogInformation(
                "FinalizePreTranscribedRawAsync: text wiped by lightweight filter (hallucination): '{Original}'",
                rawText);
            return Task.FromResult(new TranscriptionResult(string.Empty, 0.0f)
            {
                OriginalText = rawText,
                FilteredText = string.Empty
            });
        }

        return Task.FromResult(new TranscriptionResult(filteredText, 0.0f)
        {
            OriginalText = rawText,
            FilteredText = filteredText
        });
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeChunkRawAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0) return string.Empty;

        // Do NOT use TruncateIfTooLarge here — it keeps the LAST bytes, which would
        // drop the beginning of an individual chunk (already-spoken content) during
        // streaming transcription. Chunks are normally small (a few seconds); if one
        // ever exceeds the limit we log and skip rather than silently losing audio.
        if (audioData.Length > _options.MaxSegmentBytes)
        {
            _logger.LogWarning(
                "TranscribeChunkRawAsync: chunk too large ({Size} > {Max} bytes), skipping to avoid losing start of chunk",
                audioData.Length, _options.MaxSegmentBytes);
            return string.Empty;
        }

        try
        {
            var result = await _transcriber.TranscribeAsync(audioData, cancellationToken);
            return result.Success ? result.Text ?? string.Empty : string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TranscribeChunkRawAsync failed for chunk of {Bytes} bytes", audioData.Length);
            return string.Empty;
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
