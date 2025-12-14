using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for managing speech queue with cancellation support.
/// Ensures only one speech plays at a time and allows interruption.
/// </summary>
public interface ISpeechQueueService
{
    /// <summary>
    /// Enqueues text for speech. Will be spoken when current speech finishes.
    /// </summary>
    void Enqueue(string text, string? source = null);

    /// <summary>
    /// Whether speech is currently playing.
    /// </summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Number of items waiting in queue.
    /// </summary>
    int QueueCount { get; }

    /// <summary>
    /// Cancels currently playing speech (if any).
    /// Next item in queue will start playing.
    /// </summary>
    void CancelCurrent();

    /// <summary>
    /// Cancels current speech and clears the entire queue.
    /// </summary>
    void CancelAll();

    /// <summary>
    /// Gets the CancellationToken for current speech operation.
    /// Use this when calling TTS service.
    /// </summary>
    CancellationToken CurrentSpeechToken { get; }

    /// <summary>
    /// Call this before starting speech.
    /// Returns CancellationToken to use for the speech operation.
    /// </summary>
    CancellationToken BeginSpeaking();

    /// <summary>
    /// Call this when speech completes (success or failure).
    /// </summary>
    void EndSpeaking();
}

/// <summary>
/// Thread-safe speech queue with cancellation support.
/// </summary>
public sealed class SpeechQueueService : ISpeechQueueService, IDisposable
{
    private readonly ILogger<SpeechQueueService> _logger;
    private readonly ConcurrentQueue<(string Text, string? Source)> _queue = new();
    private readonly object _speakingLock = new();
    
    private CancellationTokenSource? _currentSpeechCts;
    private bool _isSpeaking;
    private bool _disposed;

    public SpeechQueueService(ILogger<SpeechQueueService> logger)
    {
        _logger = logger;
    }

    public bool IsSpeaking
    {
        get
        {
            lock (_speakingLock)
            {
                return _isSpeaking;
            }
        }
    }

    public int QueueCount => _queue.Count;

    public CancellationToken CurrentSpeechToken
    {
        get
        {
            lock (_speakingLock)
            {
                return _currentSpeechCts?.Token ?? CancellationToken.None;
            }
        }
    }

    public void Enqueue(string text, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        _queue.Enqueue((text, source));
        _logger.LogDebug("Speech queued from {Source}, queue size: {Count}", 
            source ?? "unknown", _queue.Count);
    }

    public CancellationToken BeginSpeaking()
    {
        lock (_speakingLock)
        {
            if (_disposed) return CancellationToken.None;
            
            // Cancel any existing speech
            _currentSpeechCts?.Cancel();
            _currentSpeechCts?.Dispose();
            
            // Create new CTS for this speech
            _currentSpeechCts = new CancellationTokenSource();
            _isSpeaking = true;
            
            _logger.LogDebug("Speech started");
            return _currentSpeechCts.Token;
        }
    }

    public void EndSpeaking()
    {
        lock (_speakingLock)
        {
            _isSpeaking = false;
            _logger.LogDebug("Speech ended, queue remaining: {Count}", _queue.Count);
        }
    }

    public void CancelCurrent()
    {
        lock (_speakingLock)
        {
            if (_currentSpeechCts != null && !_currentSpeechCts.IsCancellationRequested)
            {
                _logger.LogInformation("Cancelling current speech");
                _currentSpeechCts.Cancel();
            }
        }
    }

    public void CancelAll()
    {
        lock (_speakingLock)
        {
            _logger.LogInformation("Cancelling all speech, clearing queue of {Count} items", _queue.Count);
            
            // Cancel current
            _currentSpeechCts?.Cancel();
            
            // Clear queue
            while (_queue.TryDequeue(out _)) { }
        }
    }

    public bool TryDequeue(out (string Text, string? Source) item)
    {
        return _queue.TryDequeue(out item);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        lock (_speakingLock)
        {
            _disposed = true;
            _currentSpeechCts?.Cancel();
            _currentSpeechCts?.Dispose();
            _currentSpeechCts = null;
        }
    }
}
