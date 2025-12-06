using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for inter-agent communication through message queue.
/// </summary>
public interface IAgentHubService
{
    /// <summary>
    /// Send a message to another agent.
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>ID of created message</returns>
    Task<int> SendAsync(AgentMessageDto message, CancellationToken ct = default);

    /// <summary>
    /// Get pending messages for a specific agent.
    /// </summary>
    /// <param name="targetAgent">Agent identifier (e.g., "opencode", "claude")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of pending messages</returns>
    Task<IReadOnlyList<AgentMessageDto>> GetPendingAsync(string targetAgent, CancellationToken ct = default);

    /// <summary>
    /// Approve a message that requires user approval.
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    Task ApproveAsync(int messageId, CancellationToken ct = default);

    /// <summary>
    /// Cancel a pending message.
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    Task CancelAsync(int messageId, CancellationToken ct = default);

    /// <summary>
    /// Mark message as delivered to target agent.
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkDeliveredAsync(int messageId, CancellationToken ct = default);

    /// <summary>
    /// Mark message as processed by target agent.
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkProcessedAsync(int messageId, CancellationToken ct = default);

    /// <summary>
    /// Get all messages in queue (for user overview).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>All messages ordered by creation time</returns>
    Task<IReadOnlyList<AgentMessageDto>> GetQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Get messages awaiting user approval.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Messages with RequiresApproval=true and Status=pending</returns>
    Task<IReadOnlyList<AgentMessageDto>> GetAwaitingApprovalAsync(CancellationToken ct = default);
}
