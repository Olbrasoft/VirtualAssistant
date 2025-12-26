using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Published when an audio chunk is captured from the microphone.
/// </summary>
public record AudioChunkCapturedEvent(byte[] AudioData, float Rms);

/// <summary>
/// Published when speech is detected by VAD.
/// </summary>
public record SpeechDetectedEvent(float Rms);

/// <summary>
/// Published when speech ends (silence detected after speech).
/// </summary>
public record SpeechEndedEvent(byte[] AudioBuffer, int DurationMs);

/// <summary>
/// Published when transcription is completed.
/// </summary>
public record TranscriptionCompletedEvent(string Text, int DurationMs);

/// <summary>
/// Published after LLM routing to request action execution.
/// </summary>
public record ActionRequestedEvent(
    LlmRouterAction Action,
    string OriginalText,
    string? Response = null,
    PromptType? PromptType = null,
    string? TargetAgent = null);

/// <summary>
/// Published when voice state changes.
/// </summary>
public record VoiceStateChangedEvent(VoiceState OldState, VoiceState NewState);

/// <summary>
/// Published when user cancels transcription (e.g., Escape key).
/// </summary>
public record TranscriptionCancelledEvent;
