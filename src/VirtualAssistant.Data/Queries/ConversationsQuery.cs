using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Data.Queries;

/// <summary>
/// Query to get all conversations.
/// </summary>
public class ConversationsQuery : BaseQuery<IEnumerable<ConversationDto>>
{
    public ConversationsQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public ConversationsQuery(IMediator mediator) : base(mediator)
    {
    }

    public ConversationsQuery()
    {
    }
}
