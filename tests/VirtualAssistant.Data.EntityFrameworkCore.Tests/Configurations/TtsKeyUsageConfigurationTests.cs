using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.Configurations;

public class TtsKeyUsageConfigurationTests
{
    private VirtualAssistantDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task TtsKeyUsage_CanBeSavedAndRetrieved()
    {
        using var context = CreateInMemoryContext();

        var entity = new TtsKeyUsage
        {
            KeyName = "primary",
            CounterYear = 2026,
            CounterMonth = 5,
            MonthlyCharacterCount = 12_345,
            TotalSuccesses = 100,
            TotalFailures = 2,
            ConsecutiveFailures = 0,
            LastSuccessUtc = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            LastErrorReason = "429",
            State = 1, // RateLimited
            CooldownUntilUtc = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc)
        };

        context.TtsKeyUsages.Add(entity);
        await context.SaveChangesAsync();

        var saved = await context.TtsKeyUsages.AsNoTracking().FirstAsync();
        Assert.Equal("primary", saved.KeyName);
        Assert.Equal(12_345, saved.MonthlyCharacterCount);
        Assert.Equal(100, saved.TotalSuccesses);
        Assert.Equal(1, saved.State);
    }

    [Fact]
    public async Task TtsKeyUsage_RowsCanBeUpserted_ByKeyName()
    {
        // Mirrors EfCoreApiKeyUsageStore.SaveAsync upsert behavior: load by
        // key_name, mutate or create, save. Note: the EF Core InMemory provider
        // does NOT enforce unique-index constraints, so this test only proves
        // the upsert *logic* keeps a single row per key (looking it up before
        // inserting). The actual DB-level uniqueness is enforced by the
        // PostgreSQL ix_tts_key_usage_key_name_unique index in the migration.
        using var context = CreateInMemoryContext();

        async Task UpsertAsync(string name, long count)
        {
            var existing = await context.TtsKeyUsages.FirstOrDefaultAsync(x => x.KeyName == name);
            if (existing == null)
            {
                context.TtsKeyUsages.Add(new TtsKeyUsage
                {
                    KeyName = name,
                    CounterYear = 2026,
                    CounterMonth = 5,
                    MonthlyCharacterCount = count
                });
            }
            else
            {
                existing.MonthlyCharacterCount = count;
            }
            await context.SaveChangesAsync();
        }

        await UpsertAsync("primary", 100);
        await UpsertAsync("primary", 200);
        await UpsertAsync("primary", 300);

        var rows = await context.TtsKeyUsages.AsNoTracking().Where(x => x.KeyName == "primary").ToListAsync();
        Assert.Single(rows);
        Assert.Equal(300, rows[0].MonthlyCharacterCount);
    }
}
