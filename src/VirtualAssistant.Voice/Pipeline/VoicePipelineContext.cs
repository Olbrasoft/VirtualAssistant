using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline;

/// <summary>
/// Context object that flows through the voice processing pipeline.
/// Contains audio data, transcription results, router decisions, and control flags.
/// </summary>
public class VoicePipelineContext
{
    /// <summary>
    /// Raw audio data captured from microphone.
    /// </summary>
    public byte[]? AudioData { get; set; }

    /// <summary>
    /// Transcribed text from Whisper.
    /// </summary>
    public string? Transcription { get; set; }

    /// <summary>
    /// Text after echo filtering.
    /// </summary>
    public string? FilteredText { get; set; }

    /// <summary>
    /// If true, pipeline should stop processing (e.g., cancel command, echo detected, stop command).
    /// </summary>
    public bool ShouldStop { get; set; }

    /// <summary>
    /// LLM router action decision.
    /// </summary>
    public LlmRouterAction? RouterAction { get; set; }

    /// <summary>
    /// LLM router result (contains confidence, response, etc.).
    /// </summary>
    public LlmRouterResult? RouterResult { get; set; }

    /// <summary>
    /// Reason for stopping (for logging).
    /// </summary>
    public string? StopReason { get; set; }

    /// <summary>
    /// Indicates if repeat text intent was detected.
    /// </summary>
    public bool IsRepeatTextIntent { get; set; }

    /// <summary>
    /// Prompt type from router (for OpenCode action).
    /// </summary>
    public PromptType? PromptType { get; set; }

    /// <summary>
    /// Target agent for DispatchTask action.
    /// </summary>
    public string? TargetAgent { get; set; }

    /// <summary>
    /// Response from LLM (for Respond action).
    /// </summary>
    public string? Response { get; set; }
}
