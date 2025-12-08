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
/// Includes TTS notifications for incoming messages via batching and humanization.
/// </summary>
public class AgentHubService : IAgentHubService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<AgentHubService> _logger;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly INotificationBatchingService? _batchingService;

    public AgentHubService(
        VirtualAssistantDbContext dbContext,
        ILogger<AgentHubService> logger,
        IVirtualAssistantSpeaker speaker,
        INotificationBatchingService? batchingService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _speaker = speaker;
        _batchingService = batchingService;
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

        // Notify user via TTS (skip if user is on same workspace as target agent)
        var notificationText = BuildNotificationText(message);
        if (!string.IsNullOrEmpty(notificationText))
        {
            _logger.LogInformation("Sending TTS notification: {Text}", notificationText);
            await _speaker.SpeakAsync(notificationText, message.TargetAgent, ct);
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

    public async Task<int> StartTaskAsync(string sourceAgent, string content, string? targetAgent = null, string? sessionId = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        // Check if agent already has an InProgress response (shouldn't start new while another is active)
        var existingInProgress = await _dbContext.AgentResponses
            .Where(ar => ar.AgentName == sourceAgent)
            .Where(ar => ar.Status == AgentResponseStatus.InProgress)
            .FirstOrDefaultAsync(ct);

        if (existingInProgress != null)
        {
            // Log the duplicate attempt
            await LogMessageErrorAsync(sourceAgent, "error",
                $"Duplicate Start rejected - agent already has InProgress response",
                new { existingResponseId = existingInProgress.Id, content }, ct);

            _logger.LogWarning(
                "Duplicate Start rejected: agent {Agent} already has InProgress response {ResponseId}",
                sourceAgent, existingInProgress.Id);

            throw new InvalidOperationException(
                $"Agent '{sourceAgent}' already has an InProgress response (ID: {existingInProgress.Id}). Complete it first.");
        }

        // Create new AgentResponse with InProgress status
        var entity = new AgentResponse
        {
            AgentName = sourceAgent,
            Status = AgentResponseStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        _dbContext.AgentResponses.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Agent response started: {Id} by {Agent}, content: {Content}",
            entity.Id, sourceAgent, content);

        // Notify user via TTS (batched and humanized if service available)
        if (_batchingService != null)
        {
            _batchingService.QueueNotification(new AgentNotification
            {
                Agent = sourceAgent,
                Type = "start",
                Content = content
            });
        }
        else
        {
            await _speaker.SpeakAsync($"{sourceAgent} začíná pracovat.", sourceAgent, ct);
        }

        return entity.Id;
    }

    /// <summary>
    /// Logs a message error to the agent_message_logs table.
    /// </summary>
    private async Task LogMessageErrorAsync(string sourceAgent, string level, string message, object? context, CancellationToken ct)
    {
        try
        {
            var log = new AgentMessageLog
            {
                SourceAgent = sourceAgent,
                Level = level,
                Message = message,
                Context = context != null ? JsonSerializer.Serialize(context) : null,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AgentMessageLogs.Add(log);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log message error: {Message}", message);
        }
    }

    public async Task SendProgressAsync(int parentMessageId, string content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        // In the simplified AgentResponse model, we only track Start/Complete.
        // Progress updates are logged but not stored separately.
        // Verify the response exists and is still in progress.
        var response = await _dbContext.AgentResponses.FindAsync([parentMessageId], ct);

        if (response == null)
        {
            throw new KeyNotFoundException($"AgentResponse with ID {parentMessageId} not found");
        }

        if (response.Status != AgentResponseStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot add progress to response with status '{response.Status}'");
        }

        // Just log the progress, no DB storage needed
        _logger.LogInformation(
            "Progress update for agent {Agent} (response {Id}): {Content}",
            response.AgentName, parentMessageId, content);

        await Task.CompletedTask; // Ensure async signature is satisfied
    }

    public async Task CompleteTaskAsync(int parentMessageId, string summary, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        // Find the AgentResponse by ID
        var response = await _dbContext.AgentResponses.FindAsync([parentMessageId], ct);

        if (response == null)
        {
            throw new KeyNotFoundException($"AgentResponse with ID {parentMessageId} not found");
        }

        if (response.Status != AgentResponseStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot complete response with status '{response.Status}'");
        }

        // Update to Completed
        response.Status = AgentResponseStatus.Completed;
        response.CompletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Agent response completed: {Id} for agent {Agent}, summary: {Summary}",
            response.Id, response.AgentName, summary);

        // Notify user via TTS (batched and humanized if service available)
        if (_batchingService != null)
        {
            _batchingService.QueueNotification(new AgentNotification
            {
                Agent = response.AgentName,
                Type = "complete",
                Content = summary
            });
        }
        else
        {
            await _speaker.SpeakAsync($"{response.AgentName} dokončil úkol.", response.AgentName, ct);
        }
    }

    public async Task<IReadOnlyList<AgentMessageDto>> GetActiveTasksAsync(string? sourceAgent = null, CancellationToken ct = default)
    {
        var query = _dbContext.AgentResponses
            .Where(ar => ar.Status == AgentResponseStatus.InProgress);

        if (!string.IsNullOrEmpty(sourceAgent))
        {
            query = query.Where(ar => ar.AgentName == sourceAgent);
        }

        var responses = await query
            .OrderByDescending(ar => ar.StartedAt)
            .ToListAsync(ct);

        _logger.LogDebug("Found {Count} active responses for {Agent}", responses.Count, sourceAgent ?? "all agents");

        // Map AgentResponse to AgentMessageDto for backward compatibility
        return responses.Select(ar => new AgentMessageDto
        {
            Id = ar.Id,
            SourceAgent = ar.AgentName,
            TargetAgent = string.Empty,
            MessageType = "task",
            Content = string.Empty,
            Phase = MessagePhase.Start,
            SessionId = null,
            Status = "active",
            CreatedAt = ar.StartedAt
        }).ToList();
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
            SessionId = entity.SessionId,
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
