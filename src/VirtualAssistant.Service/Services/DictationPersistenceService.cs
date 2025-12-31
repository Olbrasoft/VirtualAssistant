using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;
using VirtualAssistant.Data;

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

    public DictationPersistenceService(
        ILogger<DictationPersistenceService> logger,
        IWhisperTranscriptionRepository whisperRepository,
        ILlmCorrectionRepository llmRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _whisperRepository = whisperRepository ?? throw new ArgumentNullException(nameof(whisperRepository));
        _llmRepository = llmRepository ?? throw new ArgumentNullException(nameof(llmRepository));
    }

    /// <inheritdoc />
    public async Task<int?> SaveTranscriptionAsync(
        byte[] audioData,
        string originalText,
        string? correctedText,
        int llmDurationMs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Calculate audio duration from audio data (16-bit mono @ 16kHz)
            // duration_ms = (bytes / 2 bytes_per_sample) / 16000 samples_per_second * 1000 ms_per_second
            var audioDurationMs = (int)((audioData.Length / 2.0) / 16000.0 * 1000.0);

            // Save original Whisper transcription (before LLM correction)
            var transcription = await _whisperRepository.SaveAsync(
                originalText,
                durationMs: audioDurationMs,
                cancellationToken);

            _logger.LogDebug("Saved Whisper transcription to database with ID {Id}", transcription.Id);

            // If LLM correction was applied, save it to database
            if (correctedText != null && correctedText != originalText)
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

            return transcription.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Whisper transcription to database");
            return null; // Continue with dictation even if save failed
        }
    }
}
