namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers;

/// <summary>
/// Handler for querying a conversation by ID.
/// </summary>
public class ConversationByIdQueryHandler : VaDbQueryHandler<Conversation, ConversationByIdQuery, ConversationDto?>
{
    public ConversationByIdQueryHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected override async Task<ConversationDto?> GetResultToHandleAsync(ConversationByIdQuery query, CancellationToken token)
    {
        return await Entities
            .Where(c => c.Id == query.Id)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                MessageCount = c.Messages.Count
            })
            .FirstOrDefaultAsync(token);
    }
}
