using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Embedding service using OpenRouter API (OpenAI-compatible).
/// </summary>
public class OpenRouterEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<OpenRouterEmbeddingService> _logger;
    private readonly string _apiKey;

    public OpenRouterEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingSettings> settings,
        ILogger<OpenRouterEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _apiKey = _settings.GetEffectiveApiKey();

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/Olbrasoft/VirtualAssistant");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "VirtualAssistant");

            var maskedKey = _apiKey.Length > 8
                ? $"{_apiKey[..8]}...{_apiKey[^4..]}"
                : "****";
            _logger.LogInformation("OpenRouter embedding service configured with API key: {MaskedKey}", maskedKey);
        }
        else
        {
            _logger.LogWarning("OpenRouter embedding service not configured - no API key found. " +
                "Set Embeddings:ApiKey or Embeddings:ApiKeyFile in configuration.");
        }
    }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    /// <inheritdoc />
    public async Task<Vector?> GenerateEmbeddingAsync(string? text, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Embedding service not configured, skipping embedding generation");
            return null;
        }

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
            var request = new EmbeddingRequest
            {
                Model = _settings.Model,
                Input = text
            };

            var url = $"{_settings.BaseUrl.TrimEnd('/')}/embeddings";
            var response = await _httpClient.PostAsJsonAsync(url, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenRouter API error: {Status} - {Error}",
                    response.StatusCode, errorContent);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
            if (result?.Data == null || result.Data.Count == 0)
            {
                _logger.LogWarning("Empty embedding response from OpenRouter");
                return null;
            }

            var embedding = result.Data[0].Embedding;
            _logger.LogDebug("Generated embedding with {Dims} dimensions for text of {Length} chars",
                embedding.Length, text.Length);

            return new Vector(embedding);
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

        if (!IsConfigured)
        {
            _logger.LogWarning("Embedding service not configured, skipping batch embedding generation");
            for (int i = 0; i < texts.Count; i++)
                results[i] = null;
            return results;
        }

        // Filter and prepare texts for batching
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

        // Process in batches
        var batches = validTexts
            .Chunk(_settings.BatchSize)
            .ToList();

        _logger.LogInformation("Generating embeddings for {Count} texts in {Batches} batch(es)",
            validTexts.Count, batches.Count);

        foreach (var batch in batches)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var request = new EmbeddingRequest
                {
                    Model = _settings.Model,
                    Input = batch.Select(b => b.Text).ToList()
                };

                var url = $"{_settings.BaseUrl.TrimEnd('/')}/embeddings";
                var response = await _httpClient.PostAsJsonAsync(url, request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("OpenRouter API error in batch: {Status} - {Error}",
                        response.StatusCode, errorContent);

                    // Mark all in batch as failed
                    foreach (var item in batch)
                        results[item.Index] = null;
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
                if (result?.Data == null)
                {
                    foreach (var item in batch)
                        results[item.Index] = null;
                    continue;
                }

                // Match results to original indices
                for (int i = 0; i < batch.Length && i < result.Data.Count; i++)
                {
                    var embedding = result.Data[i].Embedding;
                    results[batch[i].Index] = new Vector(embedding);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings");
                foreach (var item in batch)
                    results[item.Index] = null;
            }

            // Small delay between batches to avoid rate limiting
            if (batches.Count > 1)
                await Task.Delay(100, ct);
        }

        return results;
    }

    #region Request/Response Models

    private class EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public object Input { get; set; } = string.Empty;
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = new();

        [JsonPropertyName("usage")]
        public EmbeddingUsage? Usage { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    private class EmbeddingUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    #endregion
}
