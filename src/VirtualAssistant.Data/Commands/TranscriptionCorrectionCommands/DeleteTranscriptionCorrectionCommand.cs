using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to delete a correction from the database.
/// </summary>
/// <param name="Id">The ID of the correction to delete. Must be greater than 0.</param>
public record DeleteTranscriptionCorrectionCommand(int Id) : ICommand<bool>;
