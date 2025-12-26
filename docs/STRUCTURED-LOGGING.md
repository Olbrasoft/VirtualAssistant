# Structured Logging Guide

This document describes structured logging patterns and best practices for VirtualAssistant.

## Table of Contents

- [Overview](#overview)
- [Correlation IDs](#correlation-ids)
- [Message Templates](#message-templates)
- [Extension Methods](#extension-methods)
- [Best Practices](#best-practices)
- [Examples](#examples)

## Overview

**Structured logging** captures log data as structured fields instead of plain text messages. This enables:

✅ Better log querying and filtering
✅ Request tracing across services
✅ Performance analysis with metrics
✅ Automated alerting on structured data

### What is Structured Logging?

**Unstructured** (❌ Bad):
```csharp
_logger.LogInformation($"Processing {count} items in {duration}ms");
```

**Structured** (✅ Good):
```csharp
_logger.LogInformation("Processing {Count} items in {Duration}ms", count, duration);
```

The second approach creates **structured fields** (`Count`, `Duration`) that can be queried:
- Find all logs where `Count > 100`
- Calculate average `Duration`
- Group by operation type

## Correlation IDs

Every request and background operation gets a unique **correlation ID** for tracing.

### HTTP Requests

Correlation IDs are automatically assigned by `CorrelationIdMiddleware`:

```
GET /api/tts/speak
X-Correlation-ID: a3f2b1c8-4d5e-6f7g-8h9i-0j1k2l3m4n5o
```

Response includes the same ID:
```
HTTP/1.1 200 OK
X-Correlation-ID: a3f2b1c8-4d5e-6f7g-8h9i-0j1k2l3m4n5o
```

### Background Workers

For background operations, generate correlation IDs manually:

```csharp
using var scope = _logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = Guid.NewGuid().ToString("N")
});

_logger.LogInformation("Background task started");
// All logs in this scope will include the correlation ID
```

### Accessing Correlation ID

Inject `ICorrelationIdProvider` to access the current correlation ID:

```csharp
public class MyService
{
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public MyService(ICorrelationIdProvider correlationIdProvider)
    {
        _correlationIdProvider = correlationIdProvider;
    }

    public void DoWork()
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        // Use correlation ID for outgoing HTTP requests, etc.
    }
}
```

## Message Templates

Always use **message templates** with **named placeholders** instead of string interpolation.

### ❌ BAD - String Interpolation
```csharp
_logger.LogInformation($"User {userId} logged in at {DateTime.Now}");
```

**Why bad?**
- Not structured (can't query by userId)
- DateTime formatting is lost
- Poor performance (string allocation)

### ✅ GOOD - Message Templates
```csharp
_logger.LogInformation("User {UserId} logged in at {LoginTime}", userId, DateTime.UtcNow);
```

**Why good?**
- Structured fields: `UserId`, `LoginTime`
- Queryable: Find all logs for specific user
- Better performance (template is reused)

### Template Naming Conventions

Use **PascalCase** for placeholder names to match C# property conventions:

```csharp
_logger.LogInformation(
    "Processed {RecordCount} records in {DurationMs}ms with {ErrorCount} errors",
    recordCount,
    durationMs,
    errorCount);
```

### Destructuring Complex Objects

Use `@` prefix to destructure complex objects:

```csharp
_logger.LogInformation("Processing request {@Request}", request);
```

This logs the full object structure instead of just `ToString()`.

**Note**: Be careful not to log sensitive data (passwords, tokens, etc.)!

## Extension Methods

VirtualAssistant provides semantic extension methods for common logging scenarios.

### TTS Operations

```csharp
using VirtualAssistant.Core.Logging;

_logger.LogTtsOperation("generate", text, "Azure", durationMs: 842);
// Output: TTS operation generate for text 'Hello world' using provider Azure (took 842ms)
```

### LLM Routing

```csharp
_logger.LogLlmRouting("Mistral", "respond", durationMs: 1250, confidence: 0.95f);
// Output: LLM routing via Mistral resulted in respond with confidence 0.95 (took 1250ms)
```

### Notification Processing

```csharp
_logger.LogNotificationProcessing("claude-code", "complete", notificationId: 42, issueNumber: 339);
// Output: Processing notification 42 from agent claude-code (type: complete, issue: #339)
```

### GitHub Operations

```csharp
_logger.LogGitHubOperation("sync", issueCount: 25, durationMs: 3500, repository: "VirtualAssistant");
// Output: GitHub sync processed 25 issues in 3500ms (repo: VirtualAssistant)
```

### Audio Processing

```csharp
_logger.LogAudioProcessing("transcribe", durationMs: 2100, result: "Hello world");
// Output: Audio transcribe completed in 2100ms with result: Hello world
```

### Cache Operations

```csharp
_logger.LogCacheOperation("tts", "hit", cacheKey);
// Output: Cache tts hit for key abc123...
```

### Service Health

```csharp
_logger.LogServiceHealth("Ollama", isHealthy: true, responseTimeMs: 45);
// Output: Service Ollama health check: Healthy (response time: 45ms)

_logger.LogServiceHealth("Azure TTS", isHealthy: false, errorMessage: "Rate limit exceeded");
// Output: Service Azure TTS health check: Unhealthy - Rate limit exceeded
```

### Circuit Breaker State

```csharp
_logger.LogCircuitBreakerState("AzureTTS", "Open", failureCount: 3);
// Output: Circuit breaker for provider AzureTTS changed to Open after 3 failures
```

## Best Practices

### 1. Use Extension Methods

Prefer semantic extension methods over manual message templates:

```csharp
// ❌ Manual
_logger.LogInformation("TTS operation {Op} for {Text} via {Provider}", "generate", text, "Azure");

// ✅ Extension method
_logger.LogTtsOperation("generate", text, "Azure");
```

### 2. Include Metrics

Always log **duration** for operations:

```csharp
var sw = Stopwatch.StartNew();
await DoWorkAsync();
sw.Stop();

_logger.LogInformation("Work completed in {DurationMs}ms", sw.ElapsedMilliseconds);
```

### 3. Use Appropriate Log Levels

| Level | When to Use |
|-------|-------------|
| `Trace` | Very detailed debugging (loops, iterations) |
| `Debug` | Detailed debugging (cache hits, state changes) |
| `Information` | Normal flow (requests, completions) |
| `Warning` | Recoverable issues (fallback to default, retries) |
| `Error` | Errors that don't crash the app |
| `Critical` | Application crashes, data loss |

**Example**:
```csharp
_logger.LogDebug("Cache {Operation} for key {Key}", "hit", cacheKey);
_logger.LogInformation("Request completed in {DurationMs}ms", duration);
_logger.LogWarning("Provider {Provider} unavailable, using fallback", "Azure");
_logger.LogError(ex, "Failed to process notification {NotificationId}", id);
```

### 4. Never Log Secrets

```csharp
// ❌ NEVER do this
_logger.LogInformation("API key: {ApiKey}", apiKey);

// ✅ Log safe identifiers
_logger.LogInformation("Using API key from provider {Provider}", "Azure");
```

### 5. Keep Messages in English

All log messages should be in **English** for consistency:

```csharp
// ❌ Bad
_logger.LogInformation("Zpracovávám {Count} položek", count);

// ✅ Good
_logger.LogInformation("Processing {Count} items", count);
```

### 6. Don't Log Personal Data

Avoid logging personally identifiable information (PII):

```csharp
// ❌ Don't log email, IP, names
_logger.LogInformation("User {Email} from {IpAddress}", email, ip);

// ✅ Log anonymized identifiers
_logger.LogInformation("User {UserId} completed action", userId);
```

## Examples

### Complete Request Flow

```csharp
public async Task<IActionResult> SpeakAsync([FromBody] TtsRequest request)
{
    var sw = Stopwatch.StartNew();

    _logger.LogInformation("Received TTS request for text with {Length} characters", request.Text.Length);

    // Check cache
    var cacheKey = ComputeHash(request.Text);
    _logger.LogCacheOperation("tts", "lookup", cacheKey);

    if (_cache.TryGet(cacheKey, out var cachedAudio))
    {
        _logger.LogCacheOperation("tts", "hit", cacheKey);
        _logger.LogTtsOperation("cache_hit", request.Text, durationMs: sw.ElapsedMilliseconds);
        return Ok(cachedAudio);
    }

    _logger.LogCacheOperation("tts", "miss", cacheKey);

    // Generate audio
    var audio = await _ttsService.GenerateAsync(request.Text, "Azure");
    _logger.LogTtsOperation("generate", request.Text, "Azure", durationMs: sw.ElapsedMilliseconds);

    // Cache result
    _cache.Set(cacheKey, audio);
    _logger.LogCacheOperation("tts", "set", cacheKey);

    return Ok(audio);
}
```

**Output**:
```
[14:23:45 INF] [a3f2b1c8] Received TTS request for text with 25 characters
[14:23:45 DBG] [a3f2b1c8] Cache tts lookup for key abc123...
[14:23:45 DBG] [a3f2b1c8] Cache tts miss for key abc123...
[14:23:46 INF] [a3f2b1c8] TTS operation generate for text 'Hello world...' using provider Azure (took 842ms)
[14:23:46 DBG] [a3f2b1c8] Cache tts set for key abc123...
```

### Background Worker with Correlation ID

```csharp
public class GitHubSyncWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Generate correlation ID for this sync operation
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = Guid.NewGuid().ToString("N"),
                ["WorkerName"] = "GitHubSync"
            });

            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Starting GitHub issue sync");

            try
            {
                var issues = await _githubService.FetchIssuesAsync();
                _logger.LogGitHubOperation("fetch", issues.Count, sw.ElapsedMilliseconds);

                await _embeddingService.EmbedIssuesAsync(issues);
                _logger.LogGitHubOperation("embed", issues.Count, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GitHub sync failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
        }
    }
}
```

**Output**:
```
[14:00:00 INF] [b4c3d2e1] [WorkerName: GitHubSync] Starting GitHub issue sync
[14:00:05 INF] [b4c3d2e1] [WorkerName: GitHubSync] GitHub fetch processed 42 issues in 5234ms (repo: default)
[14:00:12 INF] [b4c3d2e1] [WorkerName: GitHubSync] GitHub embed processed 42 issues in 7156ms (repo: default)
```

## Migration Checklist

When updating existing code to use structured logging:

- [ ] Replace `$"..."` string interpolation with message templates
- [ ] Use `LoggerExtensions` methods where applicable
- [ ] Add duration tracking with `Stopwatch`
- [ ] Include correlation IDs in background workers
- [ ] Convert Czech messages to English
- [ ] Remove PII/secrets from log messages
- [ ] Use appropriate log levels
- [ ] Test that logs are queryable

## References

- [Microsoft Logging Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Structured Logging Best Practices](https://messagetemplates.org/)
- [Correlation IDs in ASP.NET Core](https://andrewlock.net/using-serilog-aspnetcore-in-asp-net-core-3-reducing-log-verbosity/)
