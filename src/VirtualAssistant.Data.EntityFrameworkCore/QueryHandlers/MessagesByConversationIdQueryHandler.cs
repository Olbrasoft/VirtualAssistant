namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers;

/// <summary>
/// Handler for querying messages by conversation ID.
/// </summary>
public class MessagesByConversationIdQueryHandler : VaDbQueryHandler<Message, MessagesByConversationIdQuery, IEnumerable<MessageDto>>
{
    public MessagesByConversationIdQueryHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected override async Task<IEnumerable<MessageDto>> GetResultToHandleAsync(MessagesByConversationIdQuery query, CancellationToken token)
    {
        return await Entities
            .Where(m => m.ConversationId == query.ConversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                Content = m.Content,
                Role = m.Role,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(token);
    }
}
