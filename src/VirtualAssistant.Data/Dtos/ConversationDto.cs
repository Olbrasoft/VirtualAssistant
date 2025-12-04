namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Data transfer object for a conversation.
/// </summary>
public class ConversationDto
{
    public int Id { get; set; }
    
    public string? Title { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public int MessageCount { get; set; }
}
