using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Decorator that provides automatic fallback from primary to secondary STT provider.
/// Tracks which provider was used for database recording.
/// </summary>
public sealed class FallbackSpeechTranscriber : ISpeechTranscriber
{
    private readonly ISpeechTranscriber _primary;
    private readonly ISpeechTranscriber _fallback;
    private readonly ISpeechTranscriberFactory _factory;
    private readonly ILogger<FallbackSpeechTranscriber> _logger;
    private readonly SpeechProviderSettings _settings;
    private bool _disposed;

    /// <summary>
    /// Gets the provider ID of the last used transcriber.
    /// Used for tracking which provider was actually used in database.
    /// </summary>
    public int LastUsedProviderId { get; private set; }

    /// <summary>
    /// Gets the language code from the primary provider.
    /// </summary>
    public string Language => _primary.Language;

    /// <summary>
    /// Initializes a new instance of <see cref="FallbackSpeechTranscriber"/>.
    /// </summary>
    public FallbackSpeechTranscriber(
        ISpeechTranscriber primary,
        ISpeechTranscriber fallback,
        ISpeechTranscriberFactory factory,
        ILogger<FallbackSpeechTranscriber> logger,
        SpeechProviderSettings settings)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Transcribes audio data with automatic fallback on failure.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FallbackSpeechTranscriber));

        if (!_settings.EnableFallback)
        {
            LastUsedProviderId = _factory.GetProviderId(_settings.PrimaryProvider);
            _logger.LogDebug("Fallback disabled, using primary provider: {Provider}", _settings.PrimaryProvider);
            return await _primary.TranscribeAsync(audioData, cancellationToken);
        }

        try
        {
            var result = await _primary.TranscribeAsync(audioData, cancellationToken);

            if (result.Success)
            {
                LastUsedProviderId = _factory.GetProviderId(_settings.PrimaryProvider);
                _logger.LogDebug("Primary provider {Provider} succeeded", _settings.PrimaryProvider);
                return result;
            }

            _logger.LogWarning(
                "Primary provider {Provider} returned error: {Error}, falling back to {Fallback}",
                _settings.PrimaryProvider, result.ErrorMessage, _settings.FallbackProvider);
        }
        catch (Exception ex) when (ShouldFallback(ex))
        {
            _logger.LogWarning(
                ex,
                "Primary provider {Provider} failed with {ExceptionType}, falling back to {Fallback}",
                _settings.PrimaryProvider, ex.GetType().Name, _settings.FallbackProvider);
        }

        // Use fallback provider
        LastUsedProviderId = _factory.GetProviderId(_settings.FallbackProvider);
        _logger.LogInformation("Using fallback provider: {Provider}", _settings.FallbackProvider);
        return await _fallback.TranscribeAsync(audioData, cancellationToken);
    }

    /// <summary>
    /// Transcribes audio stream with automatic fallback on failure.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FallbackSpeechTranscriber));

        // Read stream to memory for potential retry
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioData = memoryStream.ToArray();

        return await TranscribeAsync(audioData, cancellationToken);
    }

    /// <summary>
    /// Determines if an exception should trigger fallback.
    /// </summary>
    private static bool ShouldFallback(Exception ex)
    {
        return ex is HttpRequestException        // Network error
            || ex is TaskCanceledException       // Timeout (when not user-requested cancellation)
            || ex is OperationCanceledException  // Operation cancelled
            || ex is TimeoutException;           // Explicit timeout
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogDebug("FallbackSpeechTranscriber disposed");

        // Note: We don't dispose _primary and _fallback as they are managed by DI container
    }
}
