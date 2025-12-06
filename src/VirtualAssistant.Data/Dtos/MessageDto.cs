namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Data transfer object for a message.
/// </summary>
public class MessageDto
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the conversation this message belongs to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role of the message sender (user, assistant, system).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
