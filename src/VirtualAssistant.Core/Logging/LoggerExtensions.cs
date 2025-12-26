using Microsoft.Extensions.Logging;

namespace VirtualAssistant.Core.Logging;

/// <summary>
/// Extension methods for structured logging with semantic context.
/// Always use message templates with named placeholders instead of string interpolation.
/// </summary>
/// <example>
/// Good: _logger.LogTtsOperation("generate", text, "Azure");
/// Bad: _logger.LogInformation($"TTS generate for {text} using {provider}");
/// </example>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs a TTS operation with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="operation">Operation type (generate, cache_hit, playback, etc.)</param>
    /// <param name="text">Text being processed</param>
    /// <param name="provider">TTS provider name (optional)</param>
    /// <param name="durationMs">Operation duration in milliseconds (optional)</param>
    public static void LogTtsOperation(
        this ILogger logger,
        string operation,
        string text,
        string? provider = null,
        long? durationMs = null)
    {
        if (durationMs.HasValue)
        {
            logger.LogInformation(
                "TTS operation {Operation} for text '{Text}' using provider {Provider} (took {DurationMs}ms)",
                operation,
                text.Length > 50 ? text[..50] + "..." : text,
                provider ?? "default",
                durationMs.Value);
        }
        else
        {
            logger.LogInformation(
                "TTS operation {Operation} for text '{Text}' using provider {Provider}",
                operation,
                text.Length > 50 ? text[..50] + "..." : text,
                provider ?? "default");
        }
    }

    /// <summary>
    /// Logs an LLM routing decision with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="provider">LLM provider name</param>
    /// <param name="action">Routing action (respond, opencode, bash, etc.)</param>
    /// <param name="durationMs">Request duration in milliseconds</param>
    /// <param name="confidence">Confidence score (0.0-1.0, optional)</param>
    public static void LogLlmRouting(
        this ILogger logger,
        string provider,
        string action,
        long durationMs,
        float? confidence = null)
    {
        if (confidence.HasValue)
        {
            logger.LogInformation(
                "LLM routing via {Provider} resulted in {Action} with confidence {Confidence:F2} (took {DurationMs}ms)",
                provider,
                action,
                confidence.Value,
                durationMs);
        }
        else
        {
            logger.LogInformation(
                "LLM routing via {Provider} resulted in {Action} (took {DurationMs}ms)",
                provider,
                action,
                durationMs);
        }
    }

    /// <summary>
    /// Logs a notification processing event with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="agentName">Name of the agent</param>
    /// <param name="notificationType">Type of notification (start, complete, progress)</param>
    /// <param name="notificationId">Database ID of the notification (optional)</param>
    /// <param name="issueNumber">Related GitHub issue number (optional)</param>
    public static void LogNotificationProcessing(
        this ILogger logger,
        string agentName,
        string notificationType,
        int? notificationId = null,
        int? issueNumber = null)
    {
        if (notificationId.HasValue && issueNumber.HasValue)
        {
            logger.LogInformation(
                "Processing notification {NotificationId} from agent {AgentName} (type: {NotificationType}, issue: #{IssueNumber})",
                notificationId.Value,
                agentName,
                notificationType,
                issueNumber.Value);
        }
        else if (notificationId.HasValue)
        {
            logger.LogInformation(
                "Processing notification {NotificationId} from agent {AgentName} (type: {NotificationType})",
                notificationId.Value,
                agentName,
                notificationType);
        }
        else
        {
            logger.LogInformation(
                "Processing notification from agent {AgentName} (type: {NotificationType})",
                agentName,
                notificationType);
        }
    }

    /// <summary>
    /// Logs a GitHub issue sync operation with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="operation">Operation type (sync, embed, search)</param>
    /// <param name="issueCount">Number of issues processed</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="repository">Repository name (optional)</param>
    public static void LogGitHubOperation(
        this ILogger logger,
        string operation,
        int issueCount,
        long durationMs,
        string? repository = null)
    {
        logger.LogInformation(
            "GitHub {Operation} processed {IssueCount} issues in {DurationMs}ms (repo: {Repository})",
            operation,
            issueCount,
            durationMs,
            repository ?? "default");
    }

    /// <summary>
    /// Logs an audio processing event with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="operation">Operation type (capture, vad, transcribe)</param>
    /// <param name="durationMs">Audio duration in milliseconds</param>
    /// <param name="result">Operation result (optional)</param>
    public static void LogAudioProcessing(
        this ILogger logger,
        string operation,
        long durationMs,
        string? result = null)
    {
        if (result != null)
        {
            logger.LogInformation(
                "Audio {Operation} completed in {DurationMs}ms with result: {Result}",
                operation,
                durationMs,
                result);
        }
        else
        {
            logger.LogInformation(
                "Audio {Operation} completed in {DurationMs}ms",
                operation,
                durationMs);
        }
    }

    /// <summary>
    /// Logs a cache operation (hit/miss/set) with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="cacheType">Type of cache (tts, embeddings, etc.)</param>
    /// <param name="operation">Operation type (hit, miss, set, evict)</param>
    /// <param name="key">Cache key (truncated for readability)</param>
    public static void LogCacheOperation(
        this ILogger logger,
        string cacheType,
        string operation,
        string key)
    {
        var truncatedKey = key.Length > 32 ? key[..32] + "..." : key;

        logger.LogDebug(
            "Cache {CacheType} {Operation} for key {CacheKey}",
            cacheType,
            operation,
            truncatedKey);
    }

    /// <summary>
    /// Logs a service health check event with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="isHealthy">Health status</param>
    /// <param name="responseTimeMs">Response time in milliseconds (optional)</param>
    /// <param name="errorMessage">Error message if unhealthy (optional)</param>
    public static void LogServiceHealth(
        this ILogger logger,
        string serviceName,
        bool isHealthy,
        long? responseTimeMs = null,
        string? errorMessage = null)
    {
        if (isHealthy && responseTimeMs.HasValue)
        {
            logger.LogInformation(
                "Service {ServiceName} health check: {Status} (response time: {ResponseTimeMs}ms)",
                serviceName,
                "Healthy",
                responseTimeMs.Value);
        }
        else if (!isHealthy && errorMessage != null)
        {
            logger.LogWarning(
                "Service {ServiceName} health check: {Status} - {ErrorMessage}",
                serviceName,
                "Unhealthy",
                errorMessage);
        }
        else
        {
            logger.LogInformation(
                "Service {ServiceName} health check: {Status}",
                serviceName,
                isHealthy ? "Healthy" : "Unhealthy");
        }
    }

    /// <summary>
    /// Logs a provider circuit breaker event with structured context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="providerName">Name of the provider</param>
    /// <param name="state">Circuit breaker state (Open, Closed, HalfOpen)</param>
    /// <param name="failureCount">Number of consecutive failures (optional)</param>
    public static void LogCircuitBreakerState(
        this ILogger logger,
        string providerName,
        string state,
        int? failureCount = null)
    {
        if (failureCount.HasValue)
        {
            logger.LogWarning(
                "Circuit breaker for provider {ProviderName} changed to {State} after {FailureCount} failures",
                providerName,
                state,
                failureCount.Value);
        }
        else
        {
            logger.LogInformation(
                "Circuit breaker for provider {ProviderName} is now {State}",
                providerName,
                state);
        }
    }
}
