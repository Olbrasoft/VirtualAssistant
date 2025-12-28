using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of IWhisperTranscriptionRepository.
/// </summary>
public class WhisperTranscriptionRepository : IWhisperTranscriptionRepository
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<WhisperTranscriptionRepository> _logger;

    public WhisperTranscriptionRepository(VirtualAssistantDbContext dbContext, ILogger<WhisperTranscriptionRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WhisperTranscription> SaveAsync(string text, int? durationMs = null, CancellationToken ct = default)
    {
        var transcription = new WhisperTranscription
        {
            TranscribedText = text,
            AudioDurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.WhisperTranscriptions.Add(transcription);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Saved Whisper transcription {Id}: '{Text}' (duration: {DurationMs}ms)",
            transcription.Id, text.Length > 50 ? text[..50] + "..." : text, durationMs);

        return transcription;
    }

    public async Task<IReadOnlyList<WhisperTranscription>> GetRecentAsync(int count = 50, CancellationToken ct = default)
    {
        return await _dbContext.WhisperTranscriptions
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WhisperTranscription>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetRecentAsync(50, ct);
        }

        return await _dbContext.WhisperTranscriptions
            .Where(t => EF.Functions.ILike(t.TranscribedText, $"%{query}%"))
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<string?> GetLatestCorrectedTextAsync(CancellationToken ct = default)
    {
        var latestTranscription = await _dbContext.WhisperTranscriptions
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latestTranscription == null)
        {
            return null;
        }

        // Try to get LLM correction for this transcription
        var correction = await _dbContext.LlmCorrections
            .Where(c => c.WhisperTranscriptionId == latestTranscription.Id)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // Return corrected text if available, otherwise original Whisper text
        return correction?.CorrectedText ?? latestTranscription.TranscribedText;
    }
}
