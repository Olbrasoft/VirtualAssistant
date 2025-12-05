using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for detecting "repeat last text" intent using LLM.
/// Uses Mistral API for fast, simple intent detection.
/// </summary>
public interface IRepeatTextIntentService
{
    /// <summary>
    /// Checks if the user wants to repeat the last dictated text.
    /// </summary>
    /// <param name="inputText">The transcribed text to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user wants to repeat text, false otherwise</returns>
    Task<RepeatTextIntentResult> DetectIntentAsync(string inputText, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of repeat text intent detection.
/// </summary>
public record RepeatTextIntentResult
{
    public bool IsRepeatTextIntent { get; init; }
    public float Confidence { get; init; }
    public string? Reason { get; init; }
    public int ResponseTimeMs { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static RepeatTextIntentResult NotRepeat(string? reason = null, int responseTimeMs = 0)
        => new() { IsRepeatTextIntent = false, Reason = reason, ResponseTimeMs = responseTimeMs, Success = true };

    public static RepeatTextIntentResult Repeat(float confidence, string? reason, int responseTimeMs)
        => new() { IsRepeatTextIntent = true, Confidence = confidence, Reason = reason, ResponseTimeMs = responseTimeMs, Success = true };

    public static RepeatTextIntentResult Error(string errorMessage, int responseTimeMs = 0)
        => new() { Success = false, ErrorMessage = errorMessage, ResponseTimeMs = responseTimeMs };
}

/// <summary>
/// Implementation using Mistral API for intent detection.
/// </summary>
public class RepeatTextIntentService : IRepeatTextIntentService
{
    private readonly ILogger<RepeatTextIntentService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IPromptLoader _promptLoader;
    private readonly string _model;

    public RepeatTextIntentService(
        ILogger<RepeatTextIntentService> logger,
        HttpClient httpClient,
        IConfiguration configuration,
        IPromptLoader promptLoader)
    {
        _logger = logger;
        _httpClient = httpClient;
        _promptLoader = promptLoader;

        // Use fast model for simple intent detection
        _model = configuration["RepeatTextIntent:Model"] ?? "mistral-small-latest";

        // Configure HTTP client for Mistral API
        var apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
                  ?? configuration["MistralRouter:ApiKey"]
                  ?? "";

        _httpClient.BaseAddress = new Uri("https://api.mistral.ai/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10); // Fast timeout for intent detection

        var hasKey = !string.IsNullOrEmpty(apiKey);
        _logger.LogInformation("RepeatTextIntentService initialized with model {Model}, API key: {HasKey}",
            _model, hasKey ? "configured" : "MISSING");
    }

    public async Task<RepeatTextIntentResult> DetectIntentAsync(string inputText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return RepeatTextIntentResult.NotRepeat("Empty input");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Load prompt template
            string systemPrompt;
            try
            {
                systemPrompt = _promptLoader.LoadPrompt("RepeatTextIntent");
            }
            catch (FileNotFoundException)
            {
                _logger.LogError("RepeatTextIntent prompt not found");
                return RepeatTextIntentResult.Error("Prompt not found");
            }

            var userMessage = $"Analyzuj text: \"{inputText}\"";

            var request = new LlmRequest
            {
                Model = _model,
                Messages =
                [
                    new LlmMessage { Role = "system", Content = systemPrompt },
                    new LlmMessage { Role = "user", Content = userMessage }
                ],
                Temperature = 0.1f, // Low temperature for deterministic responses
                MaxTokens = 128 // Short response expected
            };

            _logger.LogDebug("Checking repeat text intent for: {Input}", inputText);

            var requestJson = JsonSerializer.Serialize(request);
            using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("chat/completions", requestContent, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                stopwatch.Stop();
                _logger.LogWarning("Rate limited during intent detection");
                // On rate limit, assume it's not a repeat intent to allow normal flow
                return RepeatTextIntentResult.NotRepeat("Rate limited - defaulting to normal flow", (int)stopwatch.ElapsedMilliseconds);
            }

            response.EnsureSuccessStatusCode();

            var llmResponse = await response.Content.ReadFromJsonAsync<LlmResponse>(cancellationToken: cancellationToken);
            stopwatch.Stop();

            var content = llmResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Empty response from LLM during intent detection");
                return RepeatTextIntentResult.NotRepeat("Empty LLM response", (int)stopwatch.ElapsedMilliseconds);
            }

            // Parse JSON response
            return ParseResponse(content, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Intent detection timed out");
            return RepeatTextIntentResult.NotRepeat("Timeout", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during intent detection");
            // On error, default to normal flow
            return RepeatTextIntentResult.NotRepeat($"Error: {ex.Message}", (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private RepeatTextIntentResult ParseResponse(string content, int responseTimeMs)
    {
        try
        {
            // Clean up content - remove markdown code blocks if present
            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join("\n", lines.Skip(1).TakeWhile(l => !l.StartsWith("```")));
            }

            var parsed = JsonSerializer.Deserialize<RepeatTextIntentResponseDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null)
            {
                return RepeatTextIntentResult.NotRepeat("Failed to parse JSON", responseTimeMs);
            }

            if (parsed.IsRepeatTextIntent)
            {
                return RepeatTextIntentResult.Repeat(parsed.Confidence, parsed.Reason, responseTimeMs);
            }

            return RepeatTextIntentResult.NotRepeat(parsed.Reason, responseTimeMs);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse intent response: {Content}", content);
            return RepeatTextIntentResult.NotRepeat($"JSON parse error: {ex.Message}", responseTimeMs);
        }
    }

    #region DTOs

    private class LlmRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required LlmMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private class LlmMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class LlmResponse
    {
        [JsonPropertyName("choices")]
        public LlmChoice[]? Choices { get; set; }
    }

    private class LlmChoice
    {
        [JsonPropertyName("message")]
        public LlmMessage? Message { get; set; }
    }

    private class RepeatTextIntentResponseDto
    {
        [JsonPropertyName("is_repeat_text_intent")]
        public bool IsRepeatTextIntent { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    #endregion
}
