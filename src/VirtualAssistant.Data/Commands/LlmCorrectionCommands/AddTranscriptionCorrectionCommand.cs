using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to add a new correction to the database.
/// </summary>
/// <param name="Correction">The correction to add.</param>
public record AddTranscriptionCorrectionCommand(TranscriptionCorrection Correction) : ICommand<bool>;
