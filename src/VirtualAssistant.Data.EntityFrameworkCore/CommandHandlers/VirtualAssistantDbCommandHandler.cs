namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers;

/// <summary>
/// Base class for command handlers in the VirtualAssistantDbContext using Entity Framework Core.
/// Default return type is bool.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public abstract class VirtualAssistantDbCommandHandler<TCommand, TEntity> : DbCommandHandler<VirtualAssistantDbContext, TEntity, TCommand, bool>
    where TCommand : ICommand<bool>
    where TEntity : class
{
    protected VirtualAssistantDbCommandHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected VirtualAssistantDbCommandHandler(IProjector projector, VirtualAssistantDbContext context) : base(projector, context)
    {
    }

    protected VirtualAssistantDbCommandHandler(IMapper mapper, VirtualAssistantDbContext context) : base(mapper, context)
    {
    }

    protected VirtualAssistantDbCommandHandler(IProjector projector, IMapper mapper, VirtualAssistantDbContext context) : base(projector, mapper, context)
    {
    }

    public override Task<bool> HandleAsync(TCommand command, CancellationToken token)
    {
        ThrowIfCommandIsNullOrCancellationRequested(command, token);
        return GetResultToHandleAsync(command, token);
    }

    protected abstract Task<bool> GetResultToHandleAsync(TCommand command, CancellationToken token);
}

/// <summary>
/// Base class for command handlers in the VirtualAssistantDbContext using Entity Framework Core.
/// Generic return type.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public abstract class VirtualAssistantDbCommandHandler<TCommand, TEntity, TResult> : DbCommandHandler<VirtualAssistantDbContext, TEntity, TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TEntity : class
{
    protected VirtualAssistantDbCommandHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected VirtualAssistantDbCommandHandler(IProjector projector, VirtualAssistantDbContext context) : base(projector, context)
    {
    }

    protected VirtualAssistantDbCommandHandler(IMapper mapper, VirtualAssistantDbContext context) : base(mapper, context)
    {
    }

    protected VirtualAssistantDbCommandHandler(IProjector projector, IMapper mapper, VirtualAssistantDbContext context) : base(projector, mapper, context)
    {
    }

    public override Task<TResult> HandleAsync(TCommand command, CancellationToken token)
    {
        ThrowIfCommandIsNullOrCancellationRequested(command, token);
        return GetResultToHandleAsync(command, token);
    }

    protected abstract Task<TResult> GetResultToHandleAsync(TCommand command, CancellationToken token);
}
