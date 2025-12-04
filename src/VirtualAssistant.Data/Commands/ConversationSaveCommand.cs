namespace VirtualAssistant.Data.Commands;

/// <summary>
/// Command to save (create or update) a conversation.
/// </summary>
public class ConversationSaveCommand : BaseCommand<long>
{
    public long Id { get; set; }
    
    public string? Title { get; set; }

    public ConversationSaveCommand(ICommandExecutor executor) : base(executor)
    {
    }

    public ConversationSaveCommand(IMediator mediator) : base(mediator)
    {
    }

    public ConversationSaveCommand()
    {
    }
}
