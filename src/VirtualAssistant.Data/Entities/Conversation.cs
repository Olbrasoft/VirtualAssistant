using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a conversation between the user and the assistant.
/// </summary>
public class Conversation : BaseEnity
{
    public string? Title { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<Message> Messages { get; set; } = [];
}
