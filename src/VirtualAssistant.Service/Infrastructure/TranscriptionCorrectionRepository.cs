using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Repository implementation for accessing transcription corrections via CQRS.
/// Uses IServiceScopeFactory to create a fresh DbContext scope per operation,
/// making it safe for concurrent use from the transcription pipeline.
/// </summary>
public class TranscriptionCorrectionRepository : ITranscriptionCorrectionRepository
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TranscriptionCorrectionRepository> _logger;

    public TranscriptionCorrectionRepository(
        IServiceScopeFactory scopeFactory,
        ILogger<TranscriptionCorrectionRepository> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TranscriptionCorrection>> GetActiveCorrectionsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

        var query = new GetActiveTranscriptionCorrectionsQuery();
        return await queryProcessor.ProcessAsync(query, ct);
    }

    /// <inheritdoc />
    public async Task TrackUsageAsync(int correctionId, CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var commandExecutor = scope.ServiceProvider.GetRequiredService<ICommandExecutor>();

            var command = new TrackCorrectionUsageCommand(CorrectionId: correctionId);
            await commandExecutor.ExecuteAsync(command, ct);
        }
        catch (Exception ex)
        {
            // Don't fail if tracking fails - just log warning
            _logger.LogWarning(ex, "Failed to track correction usage for ID {CorrectionId}", correctionId);
        }
    }
}
