namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers;

/// <summary>
/// Base class for query handlers in VirtualAssistant.
/// </summary>
public abstract class VaDbQueryHandler<TEntity, TQuery, TResult> : DbQueryHandler<VirtualAssistantDbContext, TEntity, TQuery, TResult>
    where TQuery : BaseQuery<TResult>
    where TEntity : class
{
    protected VaDbQueryHandler(VirtualAssistantDbContext context) : base(context)
    {
    }

    protected VaDbQueryHandler(IProjector projector, VirtualAssistantDbContext context) : base(projector, context)
    {
    }

    public override Task<TResult> HandleAsync(TQuery query, CancellationToken token)
    {
        ThrowIfQueryIsNullOrCancellationRequested(query, token);
        return GetResultToHandleAsync(query, token);
    }

    protected abstract Task<TResult> GetResultToHandleAsync(TQuery query, CancellationToken token);
}
