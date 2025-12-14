using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Humanizes agent notifications using LLM.
/// Uses Mistral API to transform raw agent messages into natural Czech speech.
/// </summary>
public class HumanizationService : IHumanizationService
{
    private readonly ILogger<HumanizationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IPromptLoader _promptLoader;
    private readonly string _model;

    private const string PromptName = "AgentNotificationHumanizer";

    public HumanizationService(
        ILogger<HumanizationService> logger,
        HttpClient httpClient,
        IConfiguration configuration,
        IPromptLoader promptLoader)
    {
        _logger = logger;
        _httpClient = httpClient;
        _promptLoader = promptLoader;
        _model = configuration["HumanizationService:Model"] ?? "mistral-small-latest";

        // Configure HttpClient for Mistral API
        var apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
                  ?? configuration["MistralRouter:ApiKey"]
                  ?? "";

        _httpClient.BaseAddress = new Uri("https://api.mistral.ai/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10); // Quick timeout for notifications

        var hasKey = !string.IsNullOrEmpty(apiKey);
        _logger.LogInformation("HumanizationService initialized with model {Model}, API key: {HasKey}",
            _model, hasKey ? "configured" : "MISSING");
    }

    public async Task<string?> HumanizeAsync(IReadOnlyList<AgentNotification> notifications, CancellationToken ct = default)
    {
        if (notifications.Count == 0)
        {
            return null;
        }

        // Filter out start notifications if there's a complete notification
        var filtered = FilterNotifications(notifications);
        if (filtered.Count == 0)
        {
            _logger.LogDebug("No notifications to humanize after filtering");
            return null;
        }

        try
        {
            var systemPrompt = _promptLoader.LoadPrompt(PromptName);
            var userMessage = FormatNotificationsAsJson(filtered);

            var request = new LlmRequest
            {
                Model = _model,
                Messages =
                [
                    new LlmMessage { Role = "system", Content = systemPrompt },
                    new LlmMessage { Role = "user", Content = userMessage }
                ],
                Temperature = 0.3f,
                MaxTokens = 100
            };

            _logger.LogDebug("Humanizing {Count} notifications via LLM", filtered.Count);

            var requestJson = JsonSerializer.Serialize(request);
            using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("chat/completions", requestContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LLM API returned {StatusCode}, using fallback", response.StatusCode);
                return FallbackHumanize(filtered);
            }

            var llmResponse = await response.Content.ReadFromJsonAsync<LlmResponse>(cancellationToken: ct);
            var content = llmResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Empty response from LLM, using fallback");
                return FallbackHumanize(filtered);
            }

            // LLM might return empty string for start-only notifications
            if (content == "(prázdný)" || content.Length < 3)
            {
                _logger.LogDebug("LLM returned empty/skip indicator");
                return null;
            }

            _logger.LogInformation("Humanized notification: {Text}", content);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error humanizing notifications, using fallback");
            return FallbackHumanize(filtered);
        }
    }

    /// <summary>
    /// Filters notifications to remove redundant ones.
    /// If we have both start and complete for the same agent, keep only complete.
    /// </summary>
    private static List<AgentNotification> FilterNotifications(IReadOnlyList<AgentNotification> notifications)
    {
        var result = new List<AgentNotification>();
        var completedAgents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: identify agents with complete notifications
        foreach (var n in notifications)
        {
            if (n.Type.Equals("complete", StringComparison.OrdinalIgnoreCase))
            {
                completedAgents.Add(n.Agent);
            }
        }

        // Second pass: include all except start for agents that also completed
        foreach (var n in notifications)
        {
            var isStart = n.Type.Equals("start", StringComparison.OrdinalIgnoreCase);
            if (isStart && completedAgents.Contains(n.Agent))
            {
                continue; // Skip start if we have complete
            }

            // Skip all start notifications (we don't announce task starts)
            if (isStart)
            {
                continue;
            }

            result.Add(n);
        }

        return result;
    }

    private static string FormatNotificationsAsJson(IReadOnlyList<AgentNotification> notifications)
    {
        var data = notifications.Select(n => new
        {
            agent = n.Agent.ToLowerInvariant(),
            type = n.Type.ToLowerInvariant(),
            content = n.Content
        });

        return JsonSerializer.Serialize(data);
    }

    /// <summary>
    /// Fallback humanization when LLM is unavailable.
    /// Uses simple template-based approach.
    /// </summary>
    private string FallbackHumanize(IReadOnlyList<AgentNotification> notifications)
    {
        _logger.LogDebug("Using fallback humanization for {Count} notifications", notifications.Count);

        if (notifications.Count == 0)
        {
            return null!;
        }

        // For status updates, just format the content with agent name
        var statusNotifications = notifications
            .Where(n => n.Type.Equals("status", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (statusNotifications.Count > 0)
        {
            // Combine status messages from all agents
            var messages = statusNotifications
                .Select(n => $"{FormatAgentName(n.Agent)} hlásí: {n.Content}")
                .ToList();
            return string.Join(" ", messages);
        }

        // For complete notifications
        var completeNotifications = notifications
            .Where(n => n.Type.Equals("complete", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (completeNotifications.Count == 0)
        {
            // Generic fallback - just read the content
            var first = notifications.First();
            return $"{FormatAgentName(first.Agent)}: {first.Content}";
        }

        var agents = completeNotifications
            .Select(n => FormatAgentName(n.Agent))
            .Distinct()
            .ToList();

        return agents.Count switch
        {
            1 => $"{agents[0]} je hotový.",
            2 => $"{agents[0]} a {agents[1]} jsou hotovi.",
            _ => $"{string.Join(", ", agents.Take(agents.Count - 1))} a {agents.Last()} jsou hotovi."
        };
    }

    /// <summary>
    /// Formats agent name for display (e.g., "opencode" -> "OpenCode")
    /// </summary>
    private static string FormatAgentName(string agent)
    {
        return agent.ToLowerInvariant() switch
        {
            "opencode" => "OpenCode",
            "claudecode" => "Claude Code",
            "claude-code" => "Claude Code",
            "claude" => "Claude",
            _ => agent
        };
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

    #endregion
}
