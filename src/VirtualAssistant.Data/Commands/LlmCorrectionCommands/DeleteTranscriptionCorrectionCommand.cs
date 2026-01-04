using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to delete a correction from the database.
/// </summary>
/// <param name="Id">The ID of the correction to delete.</param>
public record DeleteTranscriptionCorrectionCommand(int Id) : ICommand<bool>;
