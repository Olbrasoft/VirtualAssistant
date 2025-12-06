using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Dtos;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;

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
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AgentMessages.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Message sent: {Id} from {Source} to {Target}, type={Type}, requiresApproval={RequiresApproval}",
            entity.Id, entity.SourceAgent, entity.TargetAgent, entity.MessageType, entity.RequiresApproval);

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
            ProcessedAt = entity.ProcessedAt
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
