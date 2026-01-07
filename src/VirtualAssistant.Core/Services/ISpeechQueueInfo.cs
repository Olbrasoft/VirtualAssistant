namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides information about the TTS message queue.
/// Use this interface when you only need queue status.
/// </summary>
public interface ISpeechQueueInfo
{
    /// <summary>
    /// Number of messages waiting in TTS queue.
    /// </summary>
    int QueueCount { get; }
}
