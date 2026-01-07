using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to update an existing correction in the database.
/// Sets UpdatedAt to current time automatically.
/// </summary>
/// <param name="Correction">The correction to update. Must not be null.</param>
public record UpdateTranscriptionCorrectionCommand(TranscriptionCorrection Correction)
    : ICommand<bool>;
