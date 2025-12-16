using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;
using VirtualAssistant.LlmChain;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Humanizes agent notifications using LLM with multi-provider fallback.
/// Uses LlmChainClient to automatically failover between providers (Mistral, Cerebras, Groq, OpenRouter).
/// </summary>
public class HumanizationService : IHumanizationService
{
    private readonly ILogger<HumanizationService> _logger;
    private readonly ILlmChainClient _llmChain;
    private readonly IPromptLoader _promptLoader;

    private const string PromptName = "AgentNotificationHumanizer";

    public HumanizationService(
        ILogger<HumanizationService> logger,
        ILlmChainClient llmChain,
        IPromptLoader promptLoader)
    {
        _logger = logger;
        _llmChain = llmChain;
        _promptLoader = promptLoader;

        _logger.LogInformation("HumanizationService initialized with LlmChainClient (multi-provider fallback)");
    }

    public async Task<string?> HumanizeAsync(
        IReadOnlyList<AgentNotification> notifications,
        IReadOnlyDictionary<int, IssueSummaryInfo>? issueSummaries = null,
        CancellationToken ct = default)
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
            var userMessage = FormatNotificationsAsJson(filtered, issueSummaries);

            _logger.LogDebug("Humanizing {Count} notifications with {IssueCount} issue summaries via LlmChain",
                filtered.Count, issueSummaries?.Count ?? 0);

            var request = new LlmChainRequest
            {
                SystemPrompt = systemPrompt,
                UserMessage = userMessage,
                Temperature = 0.3f,
                MaxTokens = 150 // Increased to accommodate issue context
            };

            var result = await _llmChain.CompleteAsync(request, ct);

            if (!result.Success)
            {
                _logger.LogWarning("LlmChain failed: {Error}, using fallback", result.Error);
                return FallbackHumanize(filtered);
            }

            var content = CleanLlmResponse(result.Content);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Empty response from LlmChain, using fallback");
                return FallbackHumanize(filtered);
            }

            // LLM might return empty string for start-only notifications
            if (content == "(prázdný)" || content.Length < 3)
            {
                _logger.LogDebug("LLM returned empty/skip indicator");
                return null;
            }

            _logger.LogInformation("Humanized via {Provider} ({Key}): {Text}",
                result.ProviderName, result.KeyIdentifier, content);
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

    private static string FormatNotificationsAsJson(
        IReadOnlyList<AgentNotification> notifications,
        IReadOnlyDictionary<int, IssueSummaryInfo>? issueSummaries)
    {
        var notificationsData = notifications.Select(n => new
        {
            agent = n.Agent.ToLowerInvariant(),
            type = n.Type.ToLowerInvariant(),
            content = n.Content
        });

        // If we have issue summaries, include them in the context
        if (issueSummaries is { Count: > 0 })
        {
            var issuesData = issueSummaries.Values.Select(s => new
            {
                issueNumber = s.IssueNumber,
                title = s.CzechTitle,
                summary = s.CzechSummary,
                isOpen = s.IsOpen
            });

            var combined = new
            {
                notifications = notificationsData,
                relatedIssues = issuesData
            };

            return JsonSerializer.Serialize(combined);
        }

        return JsonSerializer.Serialize(notificationsData);
    }

    /// <summary>
    /// Fallback humanization when LLM is unavailable.
    /// Uses simple template-based approach with first-person persona.
    /// VirtualAssistant speaks as "I" - never mentions agents as external entities.
    /// </summary>
    private string FallbackHumanize(IReadOnlyList<AgentNotification> notifications)
    {
        _logger.LogDebug("Using fallback humanization for {Count} notifications", notifications.Count);

        if (notifications.Count == 0)
        {
            return null!;
        }

        // For status updates, use content directly (it's usually already in first person)
        var statusNotifications = notifications
            .Where(n => n.Type.Equals("status", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (statusNotifications.Count > 0)
        {
            // Use the content directly - it should be in first person
            var messages = statusNotifications
                .Select(n => n.Content)
                .ToList();
            return string.Join(" ", messages);
        }

        // For complete notifications
        var completeNotifications = notifications
            .Where(n => n.Type.Equals("complete", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (completeNotifications.Count == 0)
        {
            // Generic fallback - just read the content directly
            var first = notifications.First();
            return first.Content;
        }

        // Use first-person responses without mentioning agents
        return completeNotifications.Count switch
        {
            1 => "Mám hotovo.",
            2 => "Mám hotovo s oběma úkoly.",
            _ => "Mám hotovo se všemi úkoly."
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

    /// <summary>
    /// Cleans LLM response by removing thinking blocks and other artifacts.
    /// Qwen models (Cerebras, Groq) often output &lt;think&gt;...&lt;/think&gt; reasoning blocks.
    /// </summary>
    private static string? CleanLlmResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        // Remove <think>...</think> blocks (Qwen model reasoning)
        var cleaned = Regex.Replace(response, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);

        // Also handle unclosed <think> tags (model might not close them)
        cleaned = Regex.Replace(cleaned, @"<think>[\s\S]*$", "", RegexOptions.IgnoreCase);

        // Remove any other XML-like tags that shouldn't be spoken
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", "");

        return cleaned.Trim();
    }

}
