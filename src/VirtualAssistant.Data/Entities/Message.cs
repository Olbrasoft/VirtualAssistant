namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public class Message
{
    public long Id { get; set; }
    
    public long ConversationId { get; set; }
    
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// The role of the message sender (user, assistant, system).
    /// </summary>
    public string Role { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public Conversation Conversation { get; set; } = null!;
}
