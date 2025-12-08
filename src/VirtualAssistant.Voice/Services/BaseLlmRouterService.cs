using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Exceptions;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Base class for OpenAI-compatible LLM router services.
/// Contains shared logic for system prompt, parsing, and context management.
/// </summary>
public abstract class BaseLlmRouterService : ILlmRouterService
{
    protected readonly ILogger _logger;
    protected readonly HttpClient _httpClient;
    protected readonly string _model;
    protected readonly IPromptLoader _promptLoader;

    // Recent context for multi-turn awareness
    private readonly Queue<ContextEntry> _recentContext = new();
    private const int MaxContextEntries = 5;

    public abstract string ProviderName { get; }
    
    /// <summary>
    /// The LLM provider enum value for this service
    /// </summary>
    public abstract LlmProvider Provider { get; }

    protected BaseLlmRouterService(ILogger logger, HttpClient httpClient, string model, IPromptLoader promptLoader)
    {
        _logger = logger;
        _httpClient = httpClient;
        _model = model;
        _promptLoader = promptLoader;
    }

    public virtual async Task<LlmRouterResult> RouteAsync(string inputText, bool isDiscussionActive = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return LlmRouterResult.Ignored("Empty input");
        }

        var stopwatch = Stopwatch.StartNew();

        var systemPrompt = BuildSystemPrompt(isDiscussionActive);
        var userMessage = $"VirtualAssistant zachytil: \"{inputText}\"";

        var request = new LlmRequest
        {
            Model = _model,
            Messages =
            [
                new LlmMessage { Role = "system", Content = systemPrompt },
                new LlmMessage { Role = "user", Content = userMessage }
            ],
            Temperature = 0.2f,
            MaxTokens = 256
        };

        // No retry for rate limiting - throw exception for MultiProviderRouter to handle
        try
        {
            _logger.LogDebug("Sending to {Provider}: {Input}", ProviderName, inputText);

            // Use explicit StringContent to ensure Content-Length header is set (required by Cerebras)
            var requestJson = JsonSerializer.Serialize(request);
            using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("chat/completions", requestContent, cancellationToken);

            // Handle rate limiting - throw exception instead of retry
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("{Provider} rate limited (429): {Body}", ProviderName, errorBody);
                
                // Try to parse reset time from error message
                var resetAt = ParseResetTimeFromError(errorBody);
                
                throw new RateLimitException(Provider, $"{ProviderName} rate limited", resetAt ?? DateTime.UtcNow.AddMinutes(5));
            }

            response.EnsureSuccessStatusCode();

            var llmResponse = await response.Content.ReadFromJsonAsync<LlmResponse>(cancellationToken: cancellationToken);
            stopwatch.Stop();

            var content = llmResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Empty response from {Provider}", ProviderName);
                return LlmRouterResult.Error("Empty response", (int)stopwatch.ElapsedMilliseconds);
            }

            // Parse JSON response
            var result = ParseLlmResponse(content, (int)stopwatch.ElapsedMilliseconds);

            // Add to context
            AddToContext(inputText, result);

            _logger.LogInformation(
                "{Provider} routing: {Action} (confidence: {Confidence:F2}, time: {Time}ms)",
                ProviderName, result.Action, result.Confidence, result.ResponseTimeMs);

            return result;
        }
        catch (RateLimitException)
        {
            // Re-throw rate limit exceptions for MultiProviderRouter to handle
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP error calling {Provider} API", ProviderName);
            return LlmRouterResult.Error($"HTTP error: {ex.Message}", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "{Provider} API call timed out", ProviderName);
            return LlmRouterResult.Error("Timeout", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error calling {Provider} API", ProviderName);
            return LlmRouterResult.Error(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Parse reset time from rate limit error message (e.g., "Please try again in 11m51.936s")
    /// </summary>
    private static DateTime? ParseResetTimeFromError(string errorBody)
    {
        try
        {
            // Pattern: "Please try again in Xm Y.Zs" or "Please try again in Y.Zs"
            var match = Regex.Match(errorBody, @"try again in (\d+)m([\d.]+)s");
            if (match.Success)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddMinutes(minutes).AddSeconds(seconds);
            }
            
            // Pattern: just seconds
            match = Regex.Match(errorBody, @"try again in ([\d.]+)s");
            if (match.Success)
            {
                var seconds = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddSeconds(seconds);
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        
        return null;
    }

    private string BuildSystemPrompt(bool isDiscussionActive)
    {
        var now = DateTime.Now;
        var dayOfWeek = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "pondeli",
            DayOfWeek.Tuesday => "utery",
            DayOfWeek.Wednesday => "streda",
            DayOfWeek.Thursday => "ctvrtek",
            DayOfWeek.Friday => "patek",
            DayOfWeek.Saturday => "sobota",
            DayOfWeek.Sunday => "nedele",
            _ => now.DayOfWeek.ToString()
        };

        var contextSection = "";
        if (_recentContext.Count > 0)
        {
            var contextLines = _recentContext
                .Select(c => $"- [{c.Timestamp:HH:mm:ss}] \"{c.Input}\" -> {c.Action}")
                .ToList();
            contextSection = $@"

PREDCHOZI KONTEXT (poslednich {_recentContext.Count} interakci):
{string.Join("\n", contextLines)}";
        }

        // Load discussion warning if active
        var discussionWarning = "";
        if (isDiscussionActive)
        {
            try
            {
                discussionWarning = "\n\n" + _promptLoader.LoadPrompt("DiscussionActiveWarning");
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning("DiscussionActiveWarning prompt not found, using inline fallback");
                discussionWarning = @"

DULEZITE - DISKUZNI MOD JE AKTIVNI!
Uzivatel jiz zahajil diskuzi/planovani. NEPOUZIVEJ action ""start_discussion""!
Vsechny prompty posilej jako ""opencode"" s prompt_type ""Question"".
Pouze pokud uzivatel explicitne rika ""konec diskuze"" nebo ""ukoncit diskuzi"", pouzij ""end_discussion"".
";
            }
        }

        // Load main prompt template and replace placeholders
        var values = new Dictionary<string, string>
        {
            ["CurrentTime"] = now.ToString("HH:mm"),
            ["CurrentDate"] = now.ToString("d.M.yyyy"),
            ["DayOfWeek"] = dayOfWeek,
            ["RecentContext"] = contextSection,
            ["DiscussionWarning"] = discussionWarning
        };

        try
        {
            return _promptLoader.LoadPromptWithValues("VoiceRouterSystem", values);
        }
        catch (FileNotFoundException)
        {
            _logger.LogError("VoiceRouterSystem prompt not found, service cannot function properly");
            throw;
        }
    }

    private LlmRouterResult ParseLlmResponse(string content, int responseTimeMs)
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

            var parsed = JsonSerializer.Deserialize<LlmRouterResponseDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null)
            {
                return LlmRouterResult.Error("Failed to parse JSON", responseTimeMs);
            }

            var action = parsed.Action?.ToLowerInvariant() switch
            {
                "opencode" => LlmRouterAction.OpenCode,
                "respond" => LlmRouterAction.Respond,
                "ignore" => LlmRouterAction.Ignore,
                "savenote" => LlmRouterAction.SaveNote,
                "start_discussion" => LlmRouterAction.StartDiscussion,
                "end_discussion" => LlmRouterAction.EndDiscussion,
                "dispatch_task" => LlmRouterAction.DispatchTask,
                // Bash actions are now redirected to OpenCode
                "bash" => LlmRouterAction.OpenCode,
                _ => LlmRouterAction.Ignore
            };

            // Parse prompt type from string (case-insensitive)
            var promptType = parsed.PromptType?.ToLowerInvariant() switch
            {
                "command" => PromptType.Command,
                "question" => PromptType.Question,
                "acknowledgement" => PromptType.Acknowledgement,
                "confirmation" => PromptType.Confirmation,
                "continuation" => PromptType.Continuation,
                _ => PromptType.Question // Default to Question (safer)
            };

            return new LlmRouterResult
            {
                Action = action,
                PromptType = promptType,
                Confidence = parsed.Confidence,
                Reason = parsed.Reason,
                Response = parsed.Response,
                CommandForOpenCode = parsed.CommandForOpenCode,
                BashCommand = parsed.BashCommand,
                NoteTitle = parsed.NoteTitle,
                NoteContent = parsed.NoteContent,
                DiscussionTopic = parsed.DiscussionTopic,
                TargetAgent = parsed.TargetAgent ?? "claude", // Default to claude
                ResponseTimeMs = responseTimeMs,
                Success = true
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse {Provider} response: {Content}", ProviderName, content);
            return LlmRouterResult.Error($"JSON parse error: {ex.Message}", responseTimeMs);
        }
    }

    private void AddToContext(string input, LlmRouterResult result)
    {
        _recentContext.Enqueue(new ContextEntry
        {
            Input = input.Length > 100 ? input[..100] + "..." : input,
            Action = result.Action.ToString(),
            Timestamp = DateTime.Now
        });

        while (_recentContext.Count > MaxContextEntries)
        {
            _recentContext.Dequeue();
        }
    }

    private record ContextEntry
    {
        public required string Input { get; init; }
        public required string Action { get; init; }
        public DateTime Timestamp { get; init; }
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

    private class LlmRouterResponseDto
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("prompt_type")]
        public string? PromptType { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("command_for_opencode")]
        public string? CommandForOpenCode { get; set; }

        [JsonPropertyName("bash_command")]
        public string? BashCommand { get; set; }

        [JsonPropertyName("note_title")]
        public string? NoteTitle { get; set; }

        [JsonPropertyName("note_content")]
        public string? NoteContent { get; set; }

        [JsonPropertyName("discussion_topic")]
        public string? DiscussionTopic { get; set; }

        [JsonPropertyName("target_agent")]
        public string? TargetAgent { get; set; }
    }

    #endregion
}
