using Microsoft.Extensions.Logging;
using VirtualAssistant.Data;

namespace VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of ITranscriptionRepository.
/// </summary>
public class TranscriptionRepository : ITranscriptionRepository
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<TranscriptionRepository> _logger;

    public TranscriptionRepository(VirtualAssistantDbContext dbContext, ILogger<TranscriptionRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<VoiceTranscription> SaveTranscriptionAsync(string text, string? sourceApp, int? durationMs, CancellationToken ct = default)
    {
        var transcription = new VoiceTranscription
        {
            TranscribedText = text,
            SourceApp = sourceApp,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.VoiceTranscriptions.Add(transcription);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Saved transcription {Id}: '{Text}' (source: {SourceApp}, duration: {DurationMs}ms)",
            transcription.Id, text.Length > 50 ? text[..50] + "..." : text, sourceApp, durationMs);

        return transcription;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceTranscription>> GetRecentAsync(int count = 50, CancellationToken ct = default)
    {
        return await _dbContext.VoiceTranscriptions
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceTranscription>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetRecentAsync(50, ct);
        }

        return await _dbContext.VoiceTranscriptions
            .Where(t => EF.Functions.ILike(t.TranscribedText, $"%{query}%"))
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
    }
}
