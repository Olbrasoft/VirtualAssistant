using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Repository implementation for accessing transcription corrections via CQRS.
/// </summary>
public class TranscriptionCorrectionRepository : ITranscriptionCorrectionRepository
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger<TranscriptionCorrectionRepository> _logger;

    public TranscriptionCorrectionRepository(
        IQueryProcessor queryProcessor,
        ICommandExecutor commandExecutor,
        ILogger<TranscriptionCorrectionRepository> logger)
    {
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TranscriptionCorrection>> GetActiveCorrectionsAsync(CancellationToken ct = default)
    {
        var query = new GetActiveTranscriptionCorrectionsQuery();
        return await _queryProcessor.ProcessAsync(query, ct);
    }

    /// <inheritdoc />
    public async Task TrackUsageAsync(int correctionId, CancellationToken ct = default)
    {
        try
        {
            var command = new TrackCorrectionUsageCommand(CorrectionId: correctionId);
            await _commandExecutor.ExecuteAsync(command, ct);
        }
        catch (Exception ex)
        {
            // Don't fail if tracking fails - just log warning
            _logger.LogWarning(ex, "Failed to track correction usage for ID {CorrectionId}", correctionId);
        }
    }
}
