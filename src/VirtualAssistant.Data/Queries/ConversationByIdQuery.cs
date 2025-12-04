using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Data.Queries;

/// <summary>
/// Query to get a conversation by ID.
/// </summary>
public class ConversationByIdQuery : BaseQuery<ConversationDto?>
{
    public long Id { get; set; }

    public ConversationByIdQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public ConversationByIdQuery(IMediator mediator) : base(mediator)
    {
    }

    public ConversationByIdQuery()
    {
    }
}
