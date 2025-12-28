using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of transcription correction repository.
/// </summary>
public class TranscriptionCorrectionRepository : ITranscriptionCorrectionRepository
{
    private readonly VirtualAssistantDbContext _context;

    public TranscriptionCorrectionRepository(VirtualAssistantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TranscriptionCorrection>> GetActiveCorrectionsAsync(CancellationToken ct = default)
    {
        return await _context.TranscriptionCorrections
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Id)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<TranscriptionCorrection?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.TranscriptionCorrections
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(TranscriptionCorrection correction, CancellationToken ct = default)
    {
        correction.CreatedAt = DateTimeOffset.UtcNow;
        correction.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.TranscriptionCorrections.AddAsync(correction, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TranscriptionCorrection correction, CancellationToken ct = default)
    {
        correction.UpdatedAt = DateTimeOffset.UtcNow;

        _context.TranscriptionCorrections.Update(correction);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var correction = await GetByIdAsync(id, ct);
        if (correction != null)
        {
            _context.TranscriptionCorrections.Remove(correction);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task TrackUsageAsync(int correctionId, CancellationToken ct = default)
    {
        // TODO: Implement usage tracking (Phase 3 - optional)
        // Requires transcription_correction_usage table (new EF migration)
        await Task.CompletedTask;
    }
}
