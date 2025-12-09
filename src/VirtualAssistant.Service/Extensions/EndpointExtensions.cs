using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using VirtualAssistant.GitHub;
using VirtualAssistant.GitHub.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Request model for assistant speech start endpoint.
/// </summary>
public record AssistantSpeechStartRequest(string Text);

/// <summary>
/// Request model for TTS notify endpoint (from OpenCode plugin).
/// Source identifies the AI client for voice differentiation.
/// </summary>
public class TtsNotifyRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Source { get; set; }
}

/// <summary>
/// Request model for mute control endpoint (from PushToTalk service).
/// </summary>
public record MuteRequest(bool Muted);

/// <summary>
/// Extension methods for endpoint configuration.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Maps all VirtualAssistant API endpoints.
    /// </summary>
    public static WebApplication MapVirtualAssistantEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapAssistantSpeechEndpoints();
        app.MapTtsEndpoints();
        app.MapMuteEndpoints();
        app.MapGitHubEndpoints();
        app.MapGet("/health", () => Results.Ok("OK"));

        return app;
    }

    /// <summary>
    /// Maps assistant speech tracking endpoints (for echo cancellation).
    /// </summary>
    public static WebApplication MapAssistantSpeechEndpoints(this WebApplication app)
    {
        var speechTracker = app.Services.GetRequiredService<AssistantSpeechTrackerService>();

        app.MapPost("/api/assistant-speech/start", (AssistantSpeechStartRequest request) =>
        {
            Console.WriteLine($"\u001b[92;1m🗣️ TTS: \"{request.Text}\"\u001b[0m");
            speechTracker.StartSpeaking(request.Text);
            return Results.Ok(new { status = "started", text = request.Text });
        });

        app.MapPost("/api/assistant-speech/end", () =>
        {
            speechTracker.StopSpeaking();
            return Results.Ok(new { status = "ended" });
        });

        app.MapGet("/api/assistant-speech/status", () =>
        {
            return Results.Ok(new { historyCount = speechTracker.GetHistoryCount() });
        });

        return app;
    }

    /// <summary>
    /// Maps TTS endpoints (speak, notify, queue management, provider status).
    /// All TTS operations delegate to TtsService (single source of truth).
    /// </summary>
    public static WebApplication MapTtsEndpoints(this WebApplication app)
    {
        var ttsProviderChain = app.Services.GetRequiredService<ITtsProviderChain>();

        // Main speak/notify endpoints - delegate to TtsService
        app.MapPost("/api/tts/notify", async (TtsNotifyRequest request, TtsService ttsService, ILogger<Program> logger) =>
        {
            var sourceInfo = string.IsNullOrEmpty(request.Source) ? "" : $" [{request.Source}]";
            Console.WriteLine($"\u001b[96;1m📩 TTS Speak{sourceInfo}: \"{request.Text}\"\u001b[0m");
            logger.LogInformation("TTS Speak received from {Source}: {Text}", request.Source ?? "default", request.Text);

            // Fire and forget - TtsService handles queueing, caching, playback
            _ = Task.Run(async () =>
            {
                try
                {
                    await ttsService.SpeakAsync(request.Text, request.Source);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TTS processing failed");
                }
            });

            return Results.Ok(new { success = true, message = "TTS processing", text = request.Text, source = request.Source ?? "NOT_SET" });
        });
        app.MapPost("/api/tts/speak", async (TtsNotifyRequest request, TtsService ttsService, ILogger<Program> logger) =>
        {
            var sourceInfo = string.IsNullOrEmpty(request.Source) ? "" : $" [{request.Source}]";
            Console.WriteLine($"\u001b[96;1m📩 TTS Speak{sourceInfo}: \"{request.Text}\"\u001b[0m");
            logger.LogInformation("TTS Speak received from {Source}: {Text}", request.Source ?? "default", request.Text);

            // Fire and forget - TtsService handles queueing, caching, playback
            _ = Task.Run(async () =>
            {
                try
                {
                    await ttsService.SpeakAsync(request.Text, request.Source);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TTS processing failed");
                }
            });

            return Results.Ok(new { success = true, message = "TTS processing", text = request.Text, source = request.Source ?? "NOT_SET" });
        });

        // Provider status
        app.MapGet("/api/tts/providers", () =>
        {
            var statuses = ttsProviderChain.GetProvidersStatus();
            return Results.Ok(new { providers = statuses });
        });

        // Circuit breaker reset
        app.MapPost("/api/tts/reset-circuit-breaker", (string? provider) =>
        {
            ttsProviderChain.ResetCircuitBreaker(provider);
            return Results.Ok(new
            {
                success = true,
                message = provider != null ? $"Reset circuit breaker for {provider}" : "Reset all circuit breakers"
            });
        });

        // Queue status
        app.MapGet("/api/tts/queue", (TtsService ttsService) =>
        {
            return Results.Ok(new { queueCount = ttsService.QueueCount });
        });

        // Stop playback
        app.MapPost("/api/tts/stop", (TtsService ttsService, ILogger<Program> logger) =>
        {
            Console.WriteLine($"\u001b[91;1m🛑 TTS Stop requested\u001b[0m");
            logger.LogInformation("TTS Stop requested - stopping playback");
            ttsService.StopPlayback();
            return Results.Ok(new { success = true, message = "Playback stopped" });
        });

        // Flush queue
        app.MapPost("/api/tts/flush-queue", async (TtsService ttsService, ILogger<Program> logger) =>
        {
            var queueCount = ttsService.QueueCount;
            if (queueCount > 0)
            {
                Console.WriteLine($"\u001b[93;1m🔊 TTS Flush Queue: {queueCount} message(s) pending\u001b[0m");
            }
            logger.LogInformation("TTS Flush Queue requested. Queue count: {Count}", queueCount);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ttsService.FlushQueueAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error flushing TTS queue");
                }
            });

            return Results.Ok(new { success = true, message = "Flush queue initiated", queueCount });
        });

        return app;
    }

    /// <summary>
    /// Maps mute control endpoints.
    /// </summary>
    public static WebApplication MapMuteEndpoints(this WebApplication app)
    {
        var muteService = app.Services.GetRequiredService<IManualMuteService>();

        app.MapPost("/api/mute", (MuteRequest request, ILogger<Program> logger) =>
        {
            var previousState = muteService.IsMuted;
            muteService.SetMuted(request.Muted);

            var action = request.Muted ? "🔇 MUTED" : "🔊 UNMUTED";
            Console.WriteLine($"\u001b[95;1m{action} (via API)\u001b[0m");
            logger.LogInformation("Mute state changed via API: {PreviousState} -> {NewState}", previousState, request.Muted);

            return Results.Ok(new { success = true, muted = muteService.IsMuted, previousState });
        });

        app.MapGet("/api/mute", () =>
        {
            return Results.Ok(new { muted = muteService.IsMuted });
        });

        return app;
    }

    /// <summary>
    /// Maps GitHub sync and search endpoints.
    /// </summary>
    public static WebApplication MapGitHubEndpoints(this WebApplication app)
    {
        // Sync single repository
        app.MapPost("/api/github/sync/{owner}/{repo}", async (
            string owner,
            string repo,
            IGitHubSyncService syncService,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("GitHub sync requested for {Owner}/{Repo}", owner, repo);

            try
            {
                var (repoSynced, issuesSynced) = await syncService.SyncRepositoryAsync(owner, repo);

                if (!repoSynced)
                {
                    logger.LogWarning("Repository {Owner}/{Repo} not found", owner, repo);
                    return Results.NotFound(new { error = $"Repository {owner}/{repo} not found" });
                }

                logger.LogInformation("Synced repository {Owner}/{Repo}: {IssueCount} issues", owner, repo, issuesSynced);
                return Results.Ok(new { success = true, repository = $"{owner}/{repo}", issuesSynced });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error syncing {Owner}/{Repo}", owner, repo);
                return Results.Problem($"Sync failed: {ex.Message}");
            }
        });

        // Sync all repositories for owner
        app.MapPost("/api/github/sync/{owner}", async (
            string owner,
            IGitHubSyncService syncService,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("GitHub sync requested for all repos of {Owner}", owner);

            try
            {
                var (reposSynced, issuesSynced) = await syncService.SyncAllAsync(owner);

                logger.LogInformation("Synced all repositories for {Owner}: {RepoCount} repos, {IssueCount} issues",
                    owner, reposSynced, issuesSynced);
                return Results.Ok(new { success = true, owner, repositoriesSynced = reposSynced, issuesSynced });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error syncing all repos for {Owner}", owner);
                return Results.Problem($"Sync failed: {ex.Message}");
            }
        });

        // Sync status
        app.MapGet("/api/github/sync/status", (GitHubSyncBackgroundService syncBackgroundService) =>
        {
            var status = syncBackgroundService.GetStatus();
            return Results.Ok(status);
        });

        // Generate embeddings
        app.MapPost("/api/github/embeddings", async (
            IGitHubSyncService syncService,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("Embedding generation requested");

            try
            {
                var count = await syncService.GenerateMissingEmbeddingsAsync();
                logger.LogInformation("Generated embeddings for {Count} issues", count);
                return Results.Ok(new { success = true, embeddingsGenerated = count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating embeddings");
                return Results.Problem($"Embedding generation failed: {ex.Message}");
            }
        });

        // Semantic search
        app.MapGet("/api/github/search", async (
            string q,
            string? target,
            int? limit,
            IGitHubSearchService searchService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            var searchTarget = target?.ToLower() switch
            {
                "title" => SearchTarget.Title,
                "body" => SearchTarget.Body,
                _ => SearchTarget.Both
            };

            logger.LogInformation("GitHub search: q={Query}, target={Target}, limit={Limit}",
                q, searchTarget, limit ?? 10);

            try
            {
                var results = await searchService.SearchSimilarAsync(q, searchTarget, limit ?? 10, ct);
                return Results.Ok(new { query = q, target = searchTarget.ToString().ToLower(), count = results.Count, results });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching issues");
                return Results.Problem($"Search failed: {ex.Message}");
            }
        });

        // Find duplicates
        app.MapGet("/api/github/duplicates", async (
            string title,
            string? body,
            float? threshold,
            IGitHubSearchService searchService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return Results.BadRequest(new { error = "Query parameter 'title' is required" });
            }

            var effectiveThreshold = threshold ?? 0.8f;
            if (effectiveThreshold < 0 || effectiveThreshold > 1)
            {
                return Results.BadRequest(new { error = "Threshold must be between 0 and 1" });
            }

            logger.LogInformation("GitHub duplicate check: title={Title}, threshold={Threshold}",
                title, effectiveThreshold);

            try
            {
                var results = await searchService.FindDuplicatesAsync(title, body, effectiveThreshold, ct);
                return Results.Ok(new { title, threshold = effectiveThreshold, count = results.Count, duplicates = results });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error finding duplicates");
                return Results.Problem($"Duplicate check failed: {ex.Message}");
            }
        });

        // Get open issues
        app.MapGet("/api/github/issues/open/{owner}/{repo}", async (
            string owner,
            string repo,
            IGitHubSearchService searchService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var repoFullName = $"{owner}/{repo}";
            logger.LogInformation("Getting open issues for {Repo}", repoFullName);

            try
            {
                var results = await searchService.GetOpenIssuesAsync(repoFullName, ct);
                return Results.Ok(new { repository = repoFullName, count = results.Count, issues = results });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting open issues for {Repo}", repoFullName);
                return Results.Problem($"Failed to get open issues: {ex.Message}");
            }
        });

        return app;
    }
}
