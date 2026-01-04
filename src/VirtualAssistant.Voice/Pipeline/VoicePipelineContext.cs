using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline;

/// <summary>
/// Context object that flows through the voice processing pipeline.
/// Contains audio data, transcription results, router decisions, and control flags.
/// </summary>
public class VoicePipelineContext
{
    /// <summary>
    /// Raw audio data captured from microphone.
    /// <para><b>Set by:</b> VoicePipeline (initialization)</para>
    /// <para><b>Consumed by:</b> TranscriptionStage</para>
    /// </summary>
    public byte[]? AudioData { get; set; }

    /// <summary>
    /// Transcribed text from Whisper.
    /// <para><b>Set by:</b> TranscriptionStage</para>
    /// <para><b>Consumed by:</b> EchoFilterStage, LocalFilterStage, RepeatTextIntentStage, LlmRoutingStage</para>
    /// </summary>
    public string? Transcription { get; set; }

    /// <summary>
    /// Text after echo filtering.
    /// <para><b>Set by:</b> EchoFilterStage</para>
    /// <para><b>Consumed by:</b> StopCommandStage, RepeatTextIntentStage, LlmRoutingStage</para>
    /// </summary>
    public string? FilteredText { get; set; }

    /// <summary>
    /// If true, pipeline should stop processing (e.g., cancel command, echo detected, stop command).
    /// <para><b>Set by:</b> CancelCommandStage, EchoFilterStage, StopCommandStage</para>
    /// <para><b>Consumed by:</b> VoicePipeline (orchestration)</para>
    /// </summary>
    public bool ShouldStop { get; set; }

    /// <summary>
    /// LLM router action decision.
    /// <para><b>Set by:</b> LlmRoutingStage</para>
    /// <para><b>Consumed by:</b> ActionExecutionStage</para>
    /// </summary>
    public LlmRouterAction? RouterAction { get; set; }

    /// <summary>
    /// LLM router result (contains confidence, response, etc.).
    /// <para><b>Set by:</b> LlmRoutingStage</para>
    /// <para><b>Consumed by:</b> ActionExecutionStage (for logging/diagnostics)</para>
    /// </summary>
    public LlmRouterResult? RouterResult { get; set; }

    /// <summary>
    /// Reason for stopping (for logging).
    /// <para><b>Set by:</b> CancelCommandStage, EchoFilterStage, StopCommandStage</para>
    /// <para><b>Consumed by:</b> VoicePipeline (logging)</para>
    /// </summary>
    public string? StopReason { get; set; }

    /// <summary>
    /// Indicates if repeat text intent was detected.
    /// <para><b>Set by:</b> RepeatTextIntentStage</para>
    /// <para><b>Consumed by:</b> LlmRoutingStage (skip routing), ActionExecutionStage</para>
    /// </summary>
    public bool IsRepeatTextIntent { get; set; }

    /// <summary>
    /// Prompt type from router (for OpenCode action).
    /// <para><b>Set by:</b> LlmRoutingStage</para>
    /// <para><b>Consumed by:</b> ActionExecutionStage (OpenCode action)</para>
    /// </summary>
    public PromptType? PromptType { get; set; }

    /// <summary>
    /// Target agent for DispatchTask action.
    /// <para><b>Set by:</b> LlmRoutingStage</para>
    /// <para><b>Consumed by:</b> ActionExecutionStage (DispatchTask action)</para>
    /// </summary>
    public string? TargetAgent { get; set; }

    /// <summary>
    /// Response from LLM (for Respond action).
    /// <para><b>Set by:</b> LlmRoutingStage</para>
    /// <para><b>Consumed by:</b> ActionExecutionStage (Respond action)</para>
    /// </summary>
    public string? Response { get; set; }
}
