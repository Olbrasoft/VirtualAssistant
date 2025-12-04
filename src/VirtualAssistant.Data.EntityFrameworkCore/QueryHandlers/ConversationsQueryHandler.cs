namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers;

/// <summary>
/// Handler for querying all conversations.
/// </summary>
public class ConversationsQueryHandler : VaDbQueryHandler<Conversation, ConversationsQuery, IEnumerable<ConversationDto>>
{
    public ConversationsQueryHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected override async Task<IEnumerable<ConversationDto>> GetResultToHandleAsync(ConversationsQuery query, CancellationToken token)
    {
        return await Entities
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                MessageCount = c.Messages.Count
            })
            .ToListAsync(token);
    }
}
