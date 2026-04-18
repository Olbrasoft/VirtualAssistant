using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Google Speech-to-Text transcription service using Chromium Speech API.
/// </summary>
public sealed class GoogleSpeechTranscriber : ISpeechTranscriber
{
    private const string ApiUrl = "https://www.google.com/speech-api/v2/recognize";
    private const int SampleRate = 16000;

    private readonly HttpClient _httpClient;
    private readonly GoogleSpeechToTextOptions _options;
    private readonly ILogger<GoogleSpeechTranscriber> _logger;
    private bool _disposed;

    public string ProviderKey => "google";
    public string DatabaseName => "Google Speech-to-Text";

    /// <summary>
    /// Gets the language code for transcription.
    /// </summary>
    public string Language => _options.Language;

    /// <summary>
    /// Initializes a new instance of <see cref="GoogleSpeechTranscriber"/>.
    /// </summary>
    public GoogleSpeechTranscriber(
        HttpClient httpClient,
        IOptions<GoogleSpeechToTextOptions> options,
        ILogger<GoogleSpeechTranscriber> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);
    }

    /// <summary>
    /// Transcribes audio data to text using Google Speech API.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GoogleSpeechTranscriber));

        if (audioData == null || audioData.Length == 0)
            return new TranscriptionResult("Audio data cannot be empty");

        var startTime = DateTime.UtcNow;

        try
        {
            // Strip WAV header if present to get raw PCM
            var pcmData = StripWavHeader(audioData);

            var url = $"{ApiUrl}?output=json&lang={_options.Language}&key={_options.ApiKey}";

            using var content = new ByteArrayContent(pcmData);
            content.Headers.ContentType = new MediaTypeHeaderValue("audio/l16");
            content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("rate", SampleRate.ToString()));

            _logger.LogDebug("Sending {Size} bytes to Google Speech API (language: {Language})",
                pcmData.Length, _options.Language);

            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = ParseResponse(responseText);

            var duration = DateTime.UtcNow - startTime;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Google STT transcription successful: \"{Text}\" (confidence: {Confidence:P0}, {Duration}ms)",
                    result.Transcript, result.Confidence, duration.TotalMilliseconds);

                return new TranscriptionResult(result.Transcript, result.Confidence);
            }
            else
            {
                _logger.LogWarning("Google STT returned no results ({Duration}ms)", duration.TotalMilliseconds);
                return new TranscriptionResult("No speech detected");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google STT HTTP request failed");
            return new TranscriptionResult($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogInformation("Google STT transcription cancelled");
            return new TranscriptionResult("Transcription cancelled");
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Google STT request timed out after {Timeout}ms", _options.TimeoutMs);
            return new TranscriptionResult($"Request timed out after {_options.TimeoutMs}ms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google STT transcription failed");
            return new TranscriptionResult($"Transcription error: {ex.Message}");
        }
    }

    /// <summary>
    /// Transcribes audio stream to text.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GoogleSpeechTranscriber));

        if (audioStream == null)
            return new TranscriptionResult("Audio stream cannot be null");

        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioData = memoryStream.ToArray();

        return await TranscribeAsync(audioData, cancellationToken);
    }

    /// <summary>
    /// Parses Google Speech API response.
    /// Response format: Two JSON objects separated by newline, second one contains results.
    /// </summary>
    private GoogleSpeechResult ParseResponse(string responseText)
    {
        // Google API returns two JSON objects separated by newline
        // First is usually empty {"result":[]}
        // Second contains actual results
        var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("result", out var resultArray) &&
                    resultArray.ValueKind == JsonValueKind.Array &&
                    resultArray.GetArrayLength() > 0)
                {
                    var firstResult = resultArray[0];

                    if (firstResult.TryGetProperty("alternative", out var alternatives) &&
                        alternatives.ValueKind == JsonValueKind.Array &&
                        alternatives.GetArrayLength() > 0)
                    {
                        var firstAlternative = alternatives[0];

                        var transcript = firstAlternative.TryGetProperty("transcript", out var transcriptProp)
                            ? transcriptProp.GetString() ?? string.Empty
                            : string.Empty;

                        var confidence = firstAlternative.TryGetProperty("confidence", out var confidenceProp)
                            ? confidenceProp.GetSingle()
                            : 0.9f; // Default confidence if not provided

                        if (!string.IsNullOrWhiteSpace(transcript))
                        {
                            return new GoogleSpeechResult(true, transcript.Trim(), confidence);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug("Failed to parse response line: {Error}", ex.Message);
            }
        }

        return new GoogleSpeechResult(false, string.Empty, 0f);
    }

    /// <summary>
    /// Strips WAV header from audio data if present.
    /// </summary>
    private static byte[] StripWavHeader(byte[] audioData)
    {
        // Check for RIFF header
        if (audioData.Length > 44 &&
            audioData[0] == 'R' && audioData[1] == 'I' &&
            audioData[2] == 'F' && audioData[3] == 'F')
        {
            var pcmData = new byte[audioData.Length - 44];
            Array.Copy(audioData, 44, pcmData, 0, pcmData.Length);
            return pcmData;
        }
        return audioData;
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogDebug("GoogleSpeechTranscriber disposed");
    }

    /// <summary>
    /// Internal result type for parsing.
    /// </summary>
    private readonly record struct GoogleSpeechResult(bool Success, string Transcript, float Confidence);
}
