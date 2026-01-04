using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to save a successful LLM correction to the database.
/// </summary>
public class SaveLlmCorrectionCommand : BaseCommand<LlmCorrection>
{
    public SaveLlmCorrectionCommand(ICommandExecutor executor) : base(executor) { }
    public SaveLlmCorrectionCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// ID of the Whisper transcription that was corrected. Must be greater than 0.
    /// </summary>
    public int WhisperTranscriptionId { get; set; } = -1;

    /// <summary>
    /// The text after LLM correction. Must not be null or empty.
    /// </summary>
    public string CorrectedText { get; set; } = string.Empty;

    /// <summary>
    /// API call duration in milliseconds. Must be greater than 0.
    /// </summary>
    public int DurationMs { get; set; } = -1;
}
