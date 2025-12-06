using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Embedding service using local Ollama API with nomic-embed-text model.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingSettings> settings,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _logger.LogInformation(
            "Ollama embedding service configured: {BaseUrl}, model: {Model}",
            _settings.BaseUrl, _settings.Model);
    }

    /// <inheritdoc />
    /// <remarks>Ollama is always configured when running locally.</remarks>
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async Task<Vector?> GenerateEmbeddingAsync(string? text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (_settings.SkipShortContent && text.Length < _settings.MinContentLength)
        {
            _logger.LogDebug("Text too short for embedding: {Length} chars (min: {Min})",
                text.Length, _settings.MinContentLength);
            return null;
        }

        try
        {
            var request = new OllamaEmbeddingRequest
            {
                Model = _settings.Model,
                Prompt = text
            };

            var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/embeddings";
            var response = await _httpClient.PostAsJsonAsync(url, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Ollama API error: {Status} - {Error}",
                    response.StatusCode, errorContent);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(ct);
            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                _logger.LogWarning("Empty embedding response from Ollama");
                return null;
            }

            _logger.LogDebug("Generated embedding with {Dims} dimensions for text of {Length} chars",
                result.Embedding.Length, text.Length);

            return new Vector(result.Embedding);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Cannot connect to Ollama at {BaseUrl}. Is Ollama running?", _settings.BaseUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for text of {Length} chars", text.Length);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, Vector?>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string?> texts,
        CancellationToken ct = default)
    {
        var results = new Dictionary<int, Vector?>();

        // Filter and prepare texts
        var validTexts = new List<(int Index, string Text)>();
        for (int i = 0; i < texts.Count; i++)
        {
            var text = texts[i];
            if (string.IsNullOrWhiteSpace(text))
            {
                results[i] = null;
                continue;
            }

            if (_settings.SkipShortContent && text.Length < _settings.MinContentLength)
            {
                results[i] = null;
                continue;
            }

            validTexts.Add((i, text));
        }

        if (validTexts.Count == 0)
            return results;

        _logger.LogInformation("Generating embeddings for {Count} texts using Ollama", validTexts.Count);

        // Ollama doesn't support batch requests, process one at a time
        int processed = 0;
        foreach (var (index, text) in validTexts)
        {
            ct.ThrowIfCancellationRequested();

            var embedding = await GenerateEmbeddingAsync(text, ct);
            results[index] = embedding;
            processed++;

            // Log progress every 10 items
            if (processed % 10 == 0)
            {
                _logger.LogInformation("Processed {Processed}/{Total} embeddings", processed, validTexts.Count);
            }
        }

        _logger.LogInformation("Completed generating {Count} embeddings", processed);
        return results;
    }

    #region Request/Response Models

    private class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

    #endregion
}
