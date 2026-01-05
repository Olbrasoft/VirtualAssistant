using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to add a new correction to the database.
/// </summary>
/// <param name="Correction">The correction to add. Must not be null.</param>
public record AddTranscriptionCorrectionCommand(TranscriptionCorrection Correction)
    : ICommand<bool>;
