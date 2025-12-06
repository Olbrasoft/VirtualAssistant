namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Data transfer object for a conversation.
/// </summary>
public class ConversationDto
{
    /// <summary>
    /// Gets or sets the conversation ID.
    /// </summary>
    public int Id { get; set; }

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
    /// Gets or sets the number of messages in this conversation.
    /// </summary>
    public int MessageCount { get; set; }
}
