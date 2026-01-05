using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to track that a correction was applied during transcription processing.
/// Used for analytics to measure correction effectiveness.
/// </summary>
/// <param name="CorrectionId">The ID of the applied correction. Must be greater than 0.</param>
/// <param name="Context">Optional context about where the correction was used (e.g., "dictation", "continuous-listening").</param>
public record TrackCorrectionUsageCommand(
    int CorrectionId,
    string? Context = null
) : ICommand<bool>;
