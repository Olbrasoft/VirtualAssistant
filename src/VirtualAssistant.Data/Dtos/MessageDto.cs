namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Data transfer object for a message.
/// </summary>
public class MessageDto
{
    public long Id { get; set; }
    
    public long ConversationId { get; set; }
    
    public string Content { get; set; } = string.Empty;
    
    public string Role { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
}
