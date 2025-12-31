using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using VirtualAssistant.Data;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Service for persisting dictation transcriptions and LLM corrections to database.
/// Implements Single Responsibility Principle - only handles database persistence.
/// </summary>
public class DictationPersistenceService : IDictationPersistenceService
{
    private readonly ILogger<DictationPersistenceService> _logger;
    private readonly IWhisperTranscriptionRepository _whisperRepository;
    private readonly ILlmCorrectionRepository _llmRepository;
    private readonly AudioRecordingOptions _options;

    public DictationPersistenceService(
        ILogger<DictationPersistenceService> logger,
        IWhisperTranscriptionRepository whisperRepository,
        ILlmCorrectionRepository llmRepository,
        IOptions<AudioRecordingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _whisperRepository = whisperRepository ?? throw new ArgumentNullException(nameof(whisperRepository));
        _llmRepository = llmRepository ?? throw new ArgumentNullException(nameof(llmRepository));
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<int?> SaveTranscriptionAsync(
        byte[] audioData,
        string originalText,
        string? correctedText,
        int llmDurationMs,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        if (audioData is null || audioData.Length == 0)
        {
            throw new ArgumentException("Audio data cannot be null or empty.", nameof(audioData));
        }

        if (string.IsNullOrWhiteSpace(originalText))
        {
            throw new ArgumentException("Original text cannot be null or empty.", nameof(originalText));
        }

        // Validate audio data length (must be even for 16-bit samples)
        if (audioData.Length % _options.BytesPerSample != 0)
        {
            _logger.LogWarning(
                "Audio data length {Length} is not divisible by {BytesPerSample} (16-bit samples). Truncating to nearest even length.",
                audioData.Length,
                _options.BytesPerSample);
        }

        // Calculate audio duration from audio data (16-bit mono @ 16kHz)
        // duration_ms = (bytes / bytes_per_sample) / sample_rate * ms_per_second
        var sampleCount = audioData.Length / _options.BytesPerSample;
        var audioDurationMs = (int)((double)sampleCount / _options.SampleRate * AudioRecordingOptions.MillisecondsPerSecond);

        // Save original Whisper transcription (before LLM correction)
        WhisperTranscription transcription;
        try
        {
            transcription = await _whisperRepository.SaveAsync(
                originalText,
                durationMs: audioDurationMs,
                cancellationToken);

            _logger.LogDebug("Saved Whisper transcription to database with ID {Id}", transcription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Whisper transcription to database");
            return null; // Cannot continue without transcription ID
        }

        // If LLM correction was applied, save it to database
        // Note: If LLM save fails, we still return the transcription ID (graceful degradation)
        if (correctedText != null && correctedText != originalText)
        {
            try
            {
                var correction = await _llmRepository.SaveAsync(
                    whisperTranscriptionId: transcription.Id,
                    correctedText: correctedText,
                    durationMs: llmDurationMs,
                    cancellationToken);

                _logger.LogDebug(
                    "Saved LLM correction {Id} for transcription {TranscriptionId} (duration: {Duration}ms): '{Original}' → '{Corrected}'",
                    correction.Id,
                    transcription.Id,
                    llmDurationMs,
                    originalText.Length > 30 ? originalText[..30] + "..." : originalText,
                    correctedText.Length > 30 ? correctedText[..30] + "..." : correctedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save LLM correction for transcription {TranscriptionId}. Continuing with transcription ID.", transcription.Id);
                // Return transcription ID anyway - LLM correction is optional
            }
        }

        return transcription.Id;
    }
}
