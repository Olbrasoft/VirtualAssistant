using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a conversation between the user and the assistant.
/// </summary>
public class Conversation : BaseEnity
{
    /// <summary>
    /// Gets or sets the conversation title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the conversation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the conversation was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of messages in this conversation.
    /// </summary>
    public ICollection<Message> Messages { get; set; } = [];
}
