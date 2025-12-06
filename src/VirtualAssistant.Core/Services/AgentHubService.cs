using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Dtos;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Implementation of agent message hub for inter-agent communication.
/// Includes TTS notifications for incoming messages.
/// </summary>
public class AgentHubService : IAgentHubService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<AgentHubService> _logger;
    private readonly ITtsNotificationService _ttsNotificationService;

    public AgentHubService(
        VirtualAssistantDbContext dbContext,
        ILogger<AgentHubService> logger,
        ITtsNotificationService ttsNotificationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _ttsNotificationService = ttsNotificationService;
    }

    public async Task<int> SendAsync(AgentMessageDto message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.SourceAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.TargetAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.MessageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Content);

        var entity = new AgentMessage
        {
            SourceAgent = message.SourceAgent,
            TargetAgent = message.TargetAgent,
            MessageType = message.MessageType,
            Content = message.Content,
            Metadata = message.Metadata != null
                ? JsonSerializer.Serialize(message.Metadata)
                : null,
            RequiresApproval = message.RequiresApproval,
            Phase = MessagePhase.Complete, // Regular messages are standalone (not part of task workflow)
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AgentMessages.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Message sent: {Id} from {Source} to {Target}, type={Type}, phase={Phase}, requiresApproval={RequiresApproval}",
            entity.Id, entity.SourceAgent, entity.TargetAgent, entity.MessageType, entity.Phase, entity.RequiresApproval);

        // Notify user via TTS
        var notificationText = BuildNotificationText(message);
        if (!string.IsNullOrEmpty(notificationText))
        {
            _logger.LogInformation("Sending TTS notification: {Text}", notificationText);
            await _ttsNotificationService.SpeakAsync(notificationText, source: "assistant", ct);
        }

        return entity.Id;
    }

    public async Task<IReadOnlyList<AgentMessageDto>> GetPendingAsync(string targetAgent, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAgent);

        var messages = await _dbContext.AgentMessages
            .Where(m => m.TargetAgent == targetAgent)
            .Where(m => m.Status == "pending" || m.Status == "approved")
            .Where(m => !m.RequiresApproval || m.ApprovedAt != null) // Skip unapproved messages
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        _logger.LogDebug("Found {Count} pending messages for {Agent}", messages.Count, targetAgent);

        return messages.Select(MapToDto).ToList();
    }

    public async Task ApproveAsync(int messageId, CancellationToken ct = default)
    {
        var message = await GetMessageOrThrowAsync(messageId, ct);

        if (message.Status != "pending")
        {
            throw new InvalidOperationException($"Cannot approve message with status '{message.Status}'");
        }

        message.Status = "approved";
        message.ApprovedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Message {Id} approved", messageId);
    }

    public async Task CancelAsync(int messageId, CancellationToken ct = default)
    {
        var message = await GetMessageOrThrowAsync(messageId, ct);

        if (message.Status != "pending" && message.Status != "approved")
        {
            throw new InvalidOperationException($"Cannot cancel message with status '{message.Status}'");
        }

        message.Status = "cancelled";
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Message {Id} cancelled", messageId);
    }

    public async Task MarkDeliveredAsync(int messageId, CancellationToken ct = default)
    {
        var message = await GetMessageOrThrowAsync(messageId, ct);

        message.Status = "delivered";
        message.DeliveredAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Message {Id} marked as delivered", messageId);
    }

    public async Task MarkProcessedAsync(int messageId, CancellationToken ct = default)
    {
        var message = await GetMessageOrThrowAsync(messageId, ct);

        message.Status = "processed";
        message.ProcessedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Message {Id} marked as processed", messageId);
    }

    public async Task<IReadOnlyList<AgentMessageDto>> GetQueueAsync(CancellationToken ct = default)
    {
        var messages = await _dbContext.AgentMessages
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return messages.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AgentMessageDto>> GetAwaitingApprovalAsync(CancellationToken ct = default)
    {
        var messages = await _dbContext.AgentMessages
            .Where(m => m.RequiresApproval && m.Status == "pending")
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        _logger.LogDebug("Found {Count} messages awaiting approval", messages.Count);

        return messages.Select(MapToDto).ToList();
    }

    public async Task<int> StartTaskAsync(string sourceAgent, string content, string? targetAgent = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var entity = new AgentMessage
        {
            SourceAgent = sourceAgent,
            TargetAgent = targetAgent ?? string.Empty,
            MessageType = "task",
            Content = content,
            Phase = MessagePhase.Start,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AgentMessages.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task started: {Id} by {Source}, content: {Content}",
            entity.Id, sourceAgent, content);

        // Notify user via TTS
        await _ttsNotificationService.SpeakAsync(
            $"{sourceAgent} začíná pracovat.", source: "assistant", ct);

        return entity.Id;
    }

    public async Task SendProgressAsync(int parentMessageId, string content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var parent = await GetMessageOrThrowAsync(parentMessageId, ct);

        if (parent.Phase != MessagePhase.Start)
        {
            throw new InvalidOperationException($"Cannot add progress to message with phase '{parent.Phase}'");
        }

        var entity = new AgentMessage
        {
            SourceAgent = parent.SourceAgent,
            TargetAgent = parent.TargetAgent,
            MessageType = "progress",
            Content = content,
            Phase = MessagePhase.Progress,
            ParentMessageId = parentMessageId,
            Status = "delivered",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AgentMessages.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Progress update: {Id} for task {ParentId}, content: {Content}",
            entity.Id, parentMessageId, content);
    }

    public async Task CompleteTaskAsync(int parentMessageId, string summary, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        var parent = await GetMessageOrThrowAsync(parentMessageId, ct);

        if (parent.Phase != MessagePhase.Start)
        {
            throw new InvalidOperationException($"Cannot complete message with phase '{parent.Phase}'");
        }

        var entity = new AgentMessage
        {
            SourceAgent = parent.SourceAgent,
            TargetAgent = parent.TargetAgent,
            MessageType = "completion",
            Content = summary,
            Phase = MessagePhase.Complete,
            ParentMessageId = parentMessageId,
            Status = "processed",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        _dbContext.AgentMessages.Add(entity);

        // Update parent status to processed
        parent.Status = "processed";
        parent.ProcessedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task completed: {Id} for task {ParentId}, summary: {Summary}",
            entity.Id, parentMessageId, summary);

        // Notify user via TTS
        await _ttsNotificationService.SpeakAsync(
            $"{parent.SourceAgent} dokončil úkol.", source: "assistant", ct);
    }

    public async Task<IReadOnlyList<AgentMessageDto>> GetActiveTasksAsync(string? sourceAgent = null, CancellationToken ct = default)
    {
        var query = _dbContext.AgentMessages
            .Where(m => m.Phase == MessagePhase.Start)
            .Where(m => m.Status == "active");

        if (!string.IsNullOrEmpty(sourceAgent))
        {
            query = query.Where(m => m.SourceAgent == sourceAgent);
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        _logger.LogDebug("Found {Count} active tasks for {Agent}", messages.Count, sourceAgent ?? "all agents");

        return messages.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AgentMessageDto>> GetTaskHistoryAsync(int taskId, CancellationToken ct = default)
    {
        var messages = await _dbContext.AgentMessages
            .Where(m => m.Id == taskId || m.ParentMessageId == taskId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
        }

        _logger.LogDebug("Found {Count} messages for task {TaskId}", messages.Count, taskId);

        return messages.Select(MapToDto).ToList();
    }

    private async Task<AgentMessage> GetMessageOrThrowAsync(int messageId, CancellationToken ct)
    {
        var message = await _dbContext.AgentMessages.FindAsync([messageId], ct);

        if (message == null)
        {
            throw new KeyNotFoundException($"Message with ID {messageId} not found");
        }

        return message;
    }

    private static AgentMessageDto MapToDto(AgentMessage entity)
    {
        return new AgentMessageDto
        {
            Id = entity.Id,
            SourceAgent = entity.SourceAgent,
            TargetAgent = entity.TargetAgent,
            MessageType = entity.MessageType,
            Content = entity.Content,
            Metadata = entity.Metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Metadata)
                : null,
            RequiresApproval = entity.RequiresApproval,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            ApprovedAt = entity.ApprovedAt,
            DeliveredAt = entity.DeliveredAt,
            ProcessedAt = entity.ProcessedAt,
            Phase = entity.Phase,
            ParentMessageId = entity.ParentMessageId
        };
    }

    /// <summary>
    /// Builds a Czech notification text based on message type.
    /// Returns null for messages that should not trigger notifications.
    /// </summary>
    private static string? BuildNotificationText(AgentMessageDto message)
    {
        return message.MessageType?.ToLowerInvariant() switch
        {
            "completion" => "Claude dokončil úkol.",
            "task" when message.RequiresApproval => "Mám úkol pro Clauda. Schválíš odeslání?",
            "task" => $"Nový úkol pro {message.TargetAgent}.",
            "review_result" => $"{message.SourceAgent} zkontroloval kód.",
            "question" => $"Otázka od {message.SourceAgent}: {TruncateForTts(message.Content, 100)}",
            "error" => $"Chyba od {message.SourceAgent}.",
            "status" => null, // Don't announce status updates
            _ => $"Zpráva od {message.SourceAgent} pro {message.TargetAgent}."
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
