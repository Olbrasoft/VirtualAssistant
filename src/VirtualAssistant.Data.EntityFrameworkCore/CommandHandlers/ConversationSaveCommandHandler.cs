namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers;

/// <summary>
/// Handler for saving conversations.
/// </summary>
public class ConversationSaveCommandHandler : VaDbCommandHandler<ConversationSaveCommand, Conversation, int>
{
    public ConversationSaveCommandHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected override async Task<int> GetResultToHandleAsync(ConversationSaveCommand command, CancellationToken token)
    {
        Conversation conversation;

        if (command.Id > 0)
        {
            // Update existing
            conversation = await Entities.FirstAsync(c => c.Id == command.Id, token);
            conversation.Title = command.Title;
            conversation.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new
            conversation = new Conversation
            {
                Title = command.Title,
                CreatedAt = DateTime.UtcNow
            };
            await Entities.AddAsync(conversation, token);
        }

        await Context.SaveChangesAsync(token);
        return conversation.Id;
    }
}
