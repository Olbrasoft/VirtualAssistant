using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public class Message : BaseEnity
{
    public int ConversationId { get; set; }
    
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// The role of the message sender (user, assistant, system).
    /// </summary>
    public string Role { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public Conversation Conversation { get; set; } = null!;
}
