namespace VirtualAssistant.Core.Logging;

/// <summary>
/// Thread-safe correlation ID provider using AsyncLocal storage.
/// Ensures correlation IDs are preserved across async operations.
/// </summary>
public class CorrelationIdProvider : ICorrelationIdProvider
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <inheritdoc />
    public string GetCorrelationId()
    {
        if (string.IsNullOrEmpty(_correlationId.Value))
        {
            _correlationId.Value = Guid.NewGuid().ToString("N");
        }
        return _correlationId.Value;
    }

    /// <inheritdoc />
    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }
}
