using Microsoft.Extensions.Logging;
using VirtualAssistant.Data;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of ILlmCorrectionRepository.
/// </summary>
public class LlmCorrectionRepository : ILlmCorrectionRepository
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<LlmCorrectionRepository> _logger;

    public LlmCorrectionRepository(VirtualAssistantDbContext dbContext, ILogger<LlmCorrectionRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LlmCorrection> SaveAsync(
        int whisperTranscriptionId,
        string correctedText,
        int durationMs,
        CancellationToken ct = default)
    {
        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = whisperTranscriptionId,
            CorrectedText = correctedText,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.LlmCorrections.Add(correction);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Saved LLM correction {Id} for transcription {TranscriptionId} ({DurationMs}ms): '{Corrected}'",
            correction.Id, whisperTranscriptionId, durationMs,
            correctedText.Length > 50 ? correctedText[..50] + "..." : correctedText);

        return correction;
    }

    public async Task<LlmError> SaveErrorAsync(
        int whisperTranscriptionId,
        string errorMessage,
        int durationMs,
        CancellationToken ct = default)
    {
        var error = new LlmError
        {
            WhisperTranscriptionId = whisperTranscriptionId,
            ErrorMessage = errorMessage,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.LlmErrors.Add(error);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogWarning("Saved LLM error {Id} for transcription {TranscriptionId} ({DurationMs}ms): {Error}",
            error.Id, whisperTranscriptionId, durationMs, errorMessage);

        return error;
    }
}
