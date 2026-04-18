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
    private int _lastUsedProviderId;

    /// <summary>
    /// Gets the provider ID of the last used transcriber.
    /// Used for tracking which provider was actually used in database.
    /// Thread-safe via Volatile/Interlocked for concurrent access.
    /// </summary>
    public int LastUsedProviderId
    {
        get => System.Threading.Volatile.Read(ref _lastUsedProviderId);
        private set => System.Threading.Interlocked.Exchange(ref _lastUsedProviderId, value);
    }

    public string ProviderKey => _primary.ProviderKey;
    public string DatabaseName => _primary.DatabaseName;

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

        if (audioData is null)
            throw new ArgumentNullException(nameof(audioData));

        if (audioData.Length == 0)
            throw new ArgumentException("Audio data cannot be empty.", nameof(audioData));

        if (!_settings.EnableFallback)
        {
            LastUsedProviderId = _factory.GetProviderId(_settings.PrimaryProvider);
            _logger.LogDebug("Fallback disabled, using primary provider: {Provider}", _settings.PrimaryProvider);
            var primaryResult = await _primary.TranscribeAsync(audioData, cancellationToken);
            return WithProviderId(primaryResult, LastUsedProviderId);
        }

        try
        {
            var result = await _primary.TranscribeAsync(audioData, cancellationToken);

            if (result.Success)
            {
                LastUsedProviderId = _factory.GetProviderId(_settings.PrimaryProvider);
                _logger.LogDebug("Primary provider {Provider} succeeded", _settings.PrimaryProvider);
                return WithProviderId(result, LastUsedProviderId);
            }

            // Short-circuit non-provider failures (e.g., input validation, cancellation)
            var errorMessage = result.ErrorMessage ?? string.Empty;
            var loweredError = errorMessage.ToLowerInvariant();

            var isInputValidationError =
                loweredError.Contains("audio data cannot be empty") ||
                loweredError.Contains("audio data is empty") ||
                loweredError.Contains("no audio data");

            var isCancellationError =
                cancellationToken.IsCancellationRequested ||
                loweredError.Contains("transcription cancelled") ||
                loweredError.Contains("transcription canceled") ||
                loweredError.Contains("operation cancelled") ||
                loweredError.Contains("operation canceled");

            if (isInputValidationError || isCancellationError)
            {
                LastUsedProviderId = _factory.GetProviderId(_settings.PrimaryProvider);
                _logger.LogWarning(
                    "Primary provider {Provider} returned non-transient error: {Error}, not falling back",
                    _settings.PrimaryProvider, result.ErrorMessage);
                return WithProviderId(result, LastUsedProviderId);
            }

            _logger.LogWarning(
                "Primary provider {Provider} returned error: {Error}, falling back to {Fallback}",
                _settings.PrimaryProvider, result.ErrorMessage, _settings.FallbackProvider);
        }
        catch (Exception ex) when (ShouldFallback(ex, cancellationToken))
        {
            _logger.LogWarning(
                ex,
                "Primary provider {Provider} failed with {ExceptionType}, falling back to {Fallback}",
                _settings.PrimaryProvider, ex.GetType().Name, _settings.FallbackProvider);
        }

        // Use fallback provider
        LastUsedProviderId = _factory.GetProviderId(_settings.FallbackProvider);
        _logger.LogInformation("Using fallback provider: {Provider}", _settings.FallbackProvider);
        var fallbackResult = await _fallback.TranscribeAsync(audioData, cancellationToken);
        return WithProviderId(fallbackResult, LastUsedProviderId);
    }

    /// <summary>
    /// Creates a new TranscriptionResult with the SttProviderId set.
    /// </summary>
    private static TranscriptionResult WithProviderId(TranscriptionResult result, int providerId) =>
        result.Success
            ? new TranscriptionResult(result.Text, result.Confidence)
            {
                OriginalText = result.OriginalText,
                FilteredText = result.FilteredText,
                LlmDurationMs = result.LlmDurationMs,
                PromptId = result.PromptId,
                ModelId = result.ModelId,
                SttProviderId = providerId
            }
            : new TranscriptionResult(result.ErrorMessage ?? "Unknown error")
            {
                SttProviderId = providerId
            };

    /// <summary>
    /// Transcribes audio stream with automatic fallback on failure.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FallbackSpeechTranscriber));

        if (audioStream is null)
            throw new ArgumentNullException(nameof(audioStream));

        // Read stream to memory for potential retry
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioData = memoryStream.ToArray();

        return await TranscribeAsync(audioData, cancellationToken);
    }

    /// <summary>
    /// Determines if an exception should trigger fallback.
    /// User-requested cancellations (via CancellationToken) are propagated, not retried.
    /// </summary>
    private static bool ShouldFallback(Exception ex, CancellationToken cancellationToken)
    {
        // Genuine user cancellation - propagate, don't fallback
        // Check both: the token must be cancelled AND the exception must be from that token
        if (cancellationToken.IsCancellationRequested &&
            ex is OperationCanceledException oce &&
            oce.CancellationToken == cancellationToken)
        {
            return false;
        }

        return ex is HttpRequestException        // Network error
            || ex is TaskCanceledException       // Timeout (internal, not user cancellation)
            || ex is OperationCanceledException  // Timeout from HttpClient
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
