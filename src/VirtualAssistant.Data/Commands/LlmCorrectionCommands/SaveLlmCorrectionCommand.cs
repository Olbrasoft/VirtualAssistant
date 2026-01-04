using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to save a successful LLM correction to the database.
/// </summary>
/// <param name="WhisperTranscriptionId">ID of the Whisper transcription that was corrected.</param>
/// <param name="CorrectedText">The text after LLM correction.</param>
/// <param name="DurationMs">API call duration in milliseconds.</param>
public record SaveLlmCorrectionCommand(
    int WhisperTranscriptionId,
    string CorrectedText,
    int DurationMs
) : ICommand<LlmCorrection>;
