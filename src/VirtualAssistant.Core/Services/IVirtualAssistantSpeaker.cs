namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Single entry point for all TTS operations in VirtualAssistant.
/// All components must use this interface for speaking - nobody injects ITtsService directly.
/// Supports speech cancellation for interruption scenarios.
///
/// This is a composite interface that inherits from focused interfaces for ISP compliance:
/// - ISpeechPlaybackState: IsSpeaking, IsPlaying properties
/// - ISpeechQueueInfo: QueueCount property
/// - ISpeechController: SpeakAsync method
/// - ISpeechCancellation: Cancel methods and FlushQueueAsync
///
/// New code should prefer injecting the specific interface they need.
/// </summary>
public interface IVirtualAssistantSpeaker : ISpeechPlaybackState, ISpeechQueueInfo, ISpeechController, ISpeechCancellation
{
    // Composite interface - all members inherited from base interfaces
}
