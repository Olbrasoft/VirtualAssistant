using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Voice.SpeechToText;

/// <summary>
/// Adapter that wraps WhisperNetProvider to implement ISpeechTranscriber.
/// Replaces SpeechToTextGrpcClient with direct Whisper.net transcription.
/// Maps between SpeechToText models (TranscriptionRequest/SttTranscriptionResult)
/// and VirtualAssistant models (byte[]/TranscriptionResult).
/// </summary>
public sealed class WhisperNetTranscriber : ISpeechTranscriber
{
    private readonly ITranscriptionProvider _provider;
    private readonly string _language;
    private readonly string? _modelName;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of WhisperNetTranscriber.
    /// </summary>
    /// <param name="provider">Whisper.net transcription provider</param>
    /// <param name="language">Language code (e.g., "cs", "en")</param>
    /// <param name="modelName">Whisper model name (e.g., "ggml-large-v3-turbo.bin")</param>
    public WhisperNetTranscriber(
        ITranscriptionProvider provider,
        string language,
        string? modelName = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _language = language ?? "cs";
        _modelName = modelName;
    }

    /// <summary>
    /// Gets the language code for transcription.
    /// </summary>
    public string Language => _language;

    /// <summary>
    /// Transcribes audio data to text asynchronously.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Map VirtualAssistant request (byte[]) to SpeechToText request
        var request = new TranscriptionRequest
        {
            AudioData = audioData,
            Language = _language,
            ModelName = _modelName
        };

        // Call provider
        var sttResult = await _provider.TranscribeAsync(request, cancellationToken);

        // Map SttTranscriptionResult → VirtualAssistant.Core.Speech.TranscriptionResult
        if (sttResult.Success)
        {
            return new TranscriptionResult(
                sttResult.Text ?? string.Empty,
                sttResult.Confidence ?? 1.0f
            );
        }
        else
        {
            return new TranscriptionResult(
                sttResult.ErrorMessage ?? "Transcription failed"
            );
        }
    }

    /// <summary>
    /// Transcribes audio stream to text asynchronously.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Convert stream to byte array
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        return await TranscribeAsync(memoryStream.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Releases resources used by the transcriber.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Dispose provider if it's disposable
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }
}
