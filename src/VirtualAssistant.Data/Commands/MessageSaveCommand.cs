namespace VirtualAssistant.Data.Commands;

/// <summary>
/// Command to save (create) a message in a conversation.
/// </summary>
public class MessageSaveCommand : BaseCommand<int>
{
    public int ConversationId { get; set; }
    
    public string Content { get; set; } = string.Empty;
    
    public string Role { get; set; } = string.Empty;

    public MessageSaveCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public MessageSaveCommand(IMediator mediator) : base(mediator)
    {
    }

    public MessageSaveCommand()
    {
    }
}
