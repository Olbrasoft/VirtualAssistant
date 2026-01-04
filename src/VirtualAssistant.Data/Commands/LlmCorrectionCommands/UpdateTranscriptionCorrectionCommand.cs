using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to update an existing correction in the database.
/// Sets UpdatedAt to current time automatically.
/// </summary>
/// <param name="Correction">The correction to update.</param>
public record UpdateTranscriptionCorrectionCommand(TranscriptionCorrection Correction) : ICommand<bool>;
