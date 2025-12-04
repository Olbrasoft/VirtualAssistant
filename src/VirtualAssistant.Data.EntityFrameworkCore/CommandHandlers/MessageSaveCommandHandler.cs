namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers;

/// <summary>
/// Handler for saving messages.
/// </summary>
public class MessageSaveCommandHandler : VaDbCommandHandler<MessageSaveCommand, Message, int>
{
    public MessageSaveCommandHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected override async Task<int> GetResultToHandleAsync(MessageSaveCommand command, CancellationToken token)
    {
        var message = new Message
        {
            ConversationId = command.ConversationId,
            Content = command.Content,
            Role = command.Role,
            CreatedAt = DateTime.UtcNow
        };

        await Entities.AddAsync(message, token);
        await Context.SaveChangesAsync(token);

        // Update conversation's UpdatedAt
        var conversation = await Context.Conversations.FirstOrDefaultAsync(c => c.Id == command.ConversationId, token);
        if (conversation != null)
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync(token);
        }

        return message.Id;
    }
}
