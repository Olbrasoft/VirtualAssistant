namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a conversation between the user and the assistant.
/// </summary>
public class Conversation
{
    public long Id { get; set; }
    
    public string? Title { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<Message> Messages { get; set; } = [];
}
