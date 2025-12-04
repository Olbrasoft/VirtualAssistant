using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Data.Queries;

/// <summary>
/// Query to get messages by conversation ID.
/// </summary>
public class MessagesByConversationIdQuery : BaseQuery<IEnumerable<MessageDto>>
{
    public int ConversationId { get; set; }

    public MessagesByConversationIdQuery(IQueryProcessor processor) : base(processor)
    {
    }

    public MessagesByConversationIdQuery(IMediator mediator) : base(mediator)
    {
    }

    public MessagesByConversationIdQuery()
    {
    }
}
