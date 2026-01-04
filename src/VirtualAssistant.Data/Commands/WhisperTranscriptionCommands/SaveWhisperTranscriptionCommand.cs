using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.WhisperTranscriptionCommands;

/// <summary>
/// Command to save a new Whisper transcription to the database.
/// </summary>
/// <param name="Text">The transcribed text.</param>
/// <param name="DurationMs">Optional audio duration in milliseconds.</param>
public record SaveWhisperTranscriptionCommand(string Text, int? DurationMs = null) : ICommand<WhisperTranscription>;
