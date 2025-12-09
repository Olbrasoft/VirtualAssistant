using System.Collections.Concurrent;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for managing TTS message queue.
/// Messages are queued when speech lock is active and played back when released.
/// </summary>
public interface ITtsQueueService
{
    /// <summary>
    /// Enqueues a message for later playback.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="source">Source identifier (e.g., "opencode", "claudecode")</param>
    void Enqueue(string text, string? source);

    /// <summary>
    /// Tries to dequeue the next message.
    /// </summary>
    /// <param name="item">The dequeued message, if available</param>
    /// <returns>True if a message was dequeued, false if queue is empty</returns>
    bool TryDequeue(out (string Text, string? Source) item);

    /// <summary>
    /// Gets the number of messages currently in the queue.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// Thread-safe TTS message queue implementation using ConcurrentQueue.
/// </summary>
public sealed class TtsQueueService : ITtsQueueService
{
    private readonly ConcurrentQueue<(string Text, string? Source)> _queue = new();

    /// <inheritdoc />
    public void Enqueue(string text, string? source)
    {
        _queue.Enqueue((text, source));
    }

    /// <inheritdoc />
    public bool TryDequeue(out (string Text, string? Source) item)
    {
        return _queue.TryDequeue(out item);
    }

    /// <inheritdoc />
    public int Count => _queue.Count;
}
