using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// gRPC client for SpeechToText microservice.
/// Replaces local WhisperNetTranscriber with remote service call.
/// </summary>
public sealed class SpeechToTextGrpcClient : ISpeechTranscriber
{
    private readonly ILogger<SpeechToTextGrpcClient> _logger;
    private readonly GrpcChannel _channel;
    private readonly SpeechToText.Service.SpeechToText.SpeechToTextClient _grpcClient;
    private readonly string _language;
    private bool _disposed;

    public SpeechToTextGrpcClient(
        ILogger<SpeechToTextGrpcClient> logger,
        IOptions<ContinuousListenerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Default to localhost:5052
        var serviceUrl = Environment.GetEnvironmentVariable("SPEECHTOTEXT_SERVICE_URL") ?? "http://localhost:5052";
        _language = opts.WhisperLanguage ?? "cs";

        _logger.LogInformation("Initializing SpeechToText gRPC client: {Url}, Language: {Language}",
            serviceUrl, _language);

        _channel = GrpcChannel.ForAddress(serviceUrl);
        _grpcClient = new SpeechToText.Service.SpeechToText.SpeechToTextClient(_channel);
    }

    public string Language => _language;

    public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SpeechToTextGrpcClient));

        try
        {
            _logger.LogDebug("gRPC Transcribe request (audio size: {Size} bytes)", audioData.Length);

            var request = new SpeechToText.Service.TranscribeRequest
            {
                Audio = ByteString.CopyFrom(audioData),
                Language = _language
            };

            var response = await _grpcClient.TranscribeAsync(request, cancellationToken: cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("gRPC Transcribe failed: {Error}", response.ErrorMessage);
                return new TranscriptionResult(response.ErrorMessage ?? "Unknown error");
            }

            _logger.LogInformation("gRPC Transcribe success: \"{Text}\"", response.Text);

            // Map gRPC response to VirtualAssistant TranscriptionResult
            return new TranscriptionResult(response.Text, 1.0f);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC Transcribe exception");
            return new TranscriptionResult($"gRPC error: {ex.Message}");
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        return await TranscribeAsync(memoryStream.ToArray(), cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _channel?.Dispose();
        _disposed = true;
        _logger.LogDebug("SpeechToTextGrpcClient disposed");
    }
}
