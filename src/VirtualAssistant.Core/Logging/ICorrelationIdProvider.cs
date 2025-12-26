namespace VirtualAssistant.Core.Logging;

/// <summary>
/// Provides access to the current correlation ID for distributed tracing.
/// </summary>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// Gets the current correlation ID, generating one if none exists.
    /// </summary>
    string GetCorrelationId();

    /// <summary>
    /// Sets the correlation ID for the current execution context.
    /// </summary>
    void SetCorrelationId(string correlationId);
}
