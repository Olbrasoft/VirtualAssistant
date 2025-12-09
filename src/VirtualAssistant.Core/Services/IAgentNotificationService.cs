namespace VirtualAssistant.Core.Services;

/// <summary>
/// Event raised when an agent message is sent.
/// </summary>
public sealed record AgentMessageSentEvent(
    int MessageId,
    string SourceAgent,
    string TargetAgent,
    string MessageType,
    string Content,
    bool RequiresApproval);

/// <summary>
/// Event raised when an agent task is started.
/// </summary>
public sealed record AgentTaskStartedEvent(
    int ResponseId,
    string AgentName,
    string Content);

/// <summary>
/// Event raised when an agent task is completed.
/// </summary>
public sealed record AgentTaskCompletedEvent(
    int ResponseId,
    string AgentName,
    string Summary);

/// <summary>
/// Service for handling agent notification events.
/// Decouples AgentHubService from TTS infrastructure.
/// </summary>
public interface IAgentNotificationService
{
    /// <summary>
    /// Handles an agent message sent event.
    /// </summary>
    Task OnMessageSentAsync(AgentMessageSentEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Handles an agent task started event.
    /// </summary>
    Task OnTaskStartedAsync(AgentTaskStartedEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Handles an agent task completed event.
    /// </summary>
    Task OnTaskCompletedAsync(AgentTaskCompletedEvent evt, CancellationToken ct = default);
}

/// <summary>
/// TTS-based notification service for agent events.
/// Uses IVirtualAssistantSpeaker and optional batching service.
/// </summary>
public class TtsAgentNotificationService : IAgentNotificationService
{
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly INotificationBatchingService? _batchingService;

    public TtsAgentNotificationService(
        IVirtualAssistantSpeaker speaker,
        INotificationBatchingService? batchingService = null)
    {
        _speaker = speaker;
        _batchingService = batchingService;
    }

    public async Task OnMessageSentAsync(AgentMessageSentEvent evt, CancellationToken ct = default)
    {
        var notificationText = BuildNotificationText(evt);
        if (!string.IsNullOrEmpty(notificationText))
        {
            await _speaker.SpeakAsync(notificationText, evt.TargetAgent, ct);
        }
    }

    public async Task OnTaskStartedAsync(AgentTaskStartedEvent evt, CancellationToken ct = default)
    {
        if (_batchingService != null)
        {
            _batchingService.QueueNotification(new AgentNotification
            {
                Agent = evt.AgentName,
                Type = "start",
                Content = evt.Content
            });
        }
        else
        {
            await _speaker.SpeakAsync($"{evt.AgentName} začíná pracovat.", evt.AgentName, ct);
        }
    }

    public async Task OnTaskCompletedAsync(AgentTaskCompletedEvent evt, CancellationToken ct = default)
    {
        if (_batchingService != null)
        {
            _batchingService.QueueNotification(new AgentNotification
            {
                Agent = evt.AgentName,
                Type = "complete",
                Content = evt.Summary
            });
        }
        else
        {
            await _speaker.SpeakAsync($"{evt.AgentName} dokončil úkol.", evt.AgentName, ct);
        }
    }

    /// <summary>
    /// Builds a Czech notification text based on message type.
    /// Returns null for messages that should not trigger notifications.
    /// </summary>
    private static string? BuildNotificationText(AgentMessageSentEvent evt)
    {
        return evt.MessageType?.ToLowerInvariant() switch
        {
            "completion" => "Claude dokončil úkol.",
            "task" when evt.RequiresApproval => "Mám úkol pro Clauda. Schválíš odeslání?",
            "task" => $"Nový úkol pro {evt.TargetAgent}.",
            "review_result" => $"{evt.SourceAgent} zkontroloval kód.",
            "question" => $"Otázka od {evt.SourceAgent}: {TruncateForTts(evt.Content, 100)}",
            "error" => $"Chyba od {evt.SourceAgent}.",
            "status" => null, // Don't announce status updates
            _ => $"Zpráva od {evt.SourceAgent} pro {evt.TargetAgent}."
        };
    }

    /// <summary>
    /// Truncates text for TTS to avoid overly long speech.
    /// </summary>
    private static string TruncateForTts(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}

/// <summary>
/// No-op notification service for testing or when TTS is disabled.
/// </summary>
public class NullAgentNotificationService : IAgentNotificationService
{
    public Task OnMessageSentAsync(AgentMessageSentEvent evt, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task OnTaskStartedAsync(AgentTaskStartedEvent evt, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task OnTaskCompletedAsync(AgentTaskCompletedEvent evt, CancellationToken ct = default)
        => Task.CompletedTask;
}
