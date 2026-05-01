using Microsoft.EntityFrameworkCore;
using Olbrasoft.TextToSpeech.Providers.GoogleCloud;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IApiKeyUsageStore"/> for the Google Cloud
/// multi-key TTS provider. Persists per-key counters and parked-state to PostgreSQL
/// so the provider survives process restarts.
/// </summary>
/// <remarks>
/// The provider is registered as a singleton; the DbContext is scoped. We use
/// <see cref="IServiceScopeFactory"/> to open a fresh scope (and DbContext) for
/// every load/save call.
/// </remarks>
public sealed class EfCoreApiKeyUsageStore : IApiKeyUsageStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfCoreApiKeyUsageStore> _logger;

    public EfCoreApiKeyUsageStore(
        IServiceScopeFactory scopeFactory,
        ILogger<EfCoreApiKeyUsageStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ApiKeyUsageRecord?> LoadAsync(string keyName, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();

        TtsKeyUsage? row;
        try
        {
            row = await db.TtsKeyUsages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.KeyName == keyName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load TTS key usage for key {KeyName}", keyName);
            return null;
        }

        if (row == null) return null;

        return new ApiKeyUsageRecord
        {
            KeyName = row.KeyName,
            Year = row.CounterYear,
            Month = row.CounterMonth,
            MonthlyCharacterCount = row.MonthlyCharacterCount,
            TotalSuccesses = row.TotalSuccesses,
            TotalFailures = row.TotalFailures,
            ConsecutiveFailures = row.ConsecutiveFailures,
            LastSuccessUtc = row.LastSuccessUtc,
            LastErrorUtc = row.LastErrorUtc,
            LastErrorReason = row.LastErrorReason,
            State = (ApiKeyState)row.State,
            CooldownUntilUtc = row.CooldownUntilUtc
        };
    }

    public async Task SaveAsync(ApiKeyUsageRecord record, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();

        try
        {
            var row = await db.TtsKeyUsages
                .FirstOrDefaultAsync(x => x.KeyName == record.KeyName, cancellationToken)
                .ConfigureAwait(false);

            if (row == null)
            {
                row = new TtsKeyUsage { KeyName = record.KeyName };
                db.TtsKeyUsages.Add(row);
            }

            row.CounterYear = record.Year;
            row.CounterMonth = record.Month;
            row.MonthlyCharacterCount = record.MonthlyCharacterCount;
            row.TotalSuccesses = record.TotalSuccesses;
            row.TotalFailures = record.TotalFailures;
            row.ConsecutiveFailures = record.ConsecutiveFailures;
            row.LastSuccessUtc = record.LastSuccessUtc;
            row.LastErrorUtc = record.LastErrorUtc;
            row.LastErrorReason = record.LastErrorReason;
            row.State = (int)record.State;
            row.CooldownUntilUtc = record.CooldownUntilUtc;
            row.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist TTS key usage for key {KeyName} (counter={Count})",
                record.KeyName, record.MonthlyCharacterCount);
        }
    }
}
