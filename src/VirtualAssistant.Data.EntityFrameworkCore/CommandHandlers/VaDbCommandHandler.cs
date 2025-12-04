using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers;

/// <summary>
/// Base class for command handlers in VirtualAssistant.
/// </summary>
public abstract class VaDbCommandHandler<TCommand, TEntity, TResult> : DbBaseCommandHandler<VirtualAssistantDbContext, TEntity, TCommand, TResult>
    where TCommand : ICommand<TResult> 
    where TEntity : BaseEnity
{
    protected VaDbCommandHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected VaDbCommandHandler(IProjector projector, VirtualAssistantDbContext context) : base(projector, context)
    {
    }

    protected VaDbCommandHandler(IMapper mapper, VirtualAssistantDbContext context) : base(mapper, context)
    {
    }

    protected VaDbCommandHandler(IProjector projector, IMapper mapper, VirtualAssistantDbContext context) : base(projector, mapper, context)
    {
    }

    public override Task<TResult> HandleAsync(TCommand command, CancellationToken token)
    {
        ThrowIfCommandIsNullOrCancellationRequested(command, token);
        return GetResultToHandleAsync(command, token);
    }

    protected abstract Task<TResult> GetResultToHandleAsync(TCommand command, CancellationToken token);
}
