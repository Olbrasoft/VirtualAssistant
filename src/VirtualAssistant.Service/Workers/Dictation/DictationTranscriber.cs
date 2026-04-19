using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Service.Workers.Streaming;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationTranscriber : IDictationTranscriber
{
    private readonly ILogger<DictationTranscriber> _logger;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IStreamingChunkAssembler _streamingAssembler;

    public DictationTranscriber(
        ILogger<DictationTranscriber> logger,
        ITranscriptionService transcriptionService,
        IStreamingChunkAssembler streamingAssembler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _streamingAssembler = streamingAssembler ?? throw new ArgumentNullException(nameof(streamingAssembler));

        // Forward the backend's RawTranscriptionReady event so consumers
        // bind against IDictationTranscriber, not ITranscriptionService.
        _transcriptionService.RawTranscriptionReady += OnBackendRawReady;
    }

    public event Action<string>? RawTranscriptionReady;

    public bool IsStreamingActive => _streamingAssembler.IsActive;

    private void OnBackendRawReady(string text) => RawTranscriptionReady?.Invoke(text);

    public void BeginSession(bool streamingActive) => _streamingAssembler.Reset(streamingActive);

    public void ForwardChunk(int index, byte[] pcmBytes) => _streamingAssembler.SubmitChunk(index, pcmBytes);

    public void EndSession() => _streamingAssembler.CancelAndClear();

    public async Task<TranscriptionResult> TranscribeRawAsync(byte[] audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);

        if (_streamingAssembler.IsActive)
        {
            var combined = await _streamingAssembler.CombineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(combined))
            {
                _logger.LogWarning("Streaming quick transcription produced empty text; falling back to full-buffer Whisper");
                return await _transcriptionService.TranscribeRawAsync(audio, cancellationToken);
            }

            _logger.LogInformation(
                "Streaming quick transcription: combined {Count} chunks into {Length} chars",
                _streamingAssembler.CompletedChunkCount, combined.Length);
            return await _transcriptionService.FinalizePreTranscribedRawAsync(combined, cancellationToken);
        }

        return await _transcriptionService.TranscribeRawAsync(audio, cancellationToken);
    }

    public Task<TranscriptionResult> TranscribeFullAsync(byte[] audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return _transcriptionService.TranscribeAsync(audio, cancellationToken);
    }
}
