using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Service.Hubs;

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
    /// <summary>
    /// Gets or sets the text to speak via text-to-speech.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source identifier for voice differentiation (e.g., "assistant", "opencode").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Optional GitHub issue IDs related to this notification.
    /// </summary>
    public List<int>? IssueIds { get; set; }
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
        // Enable static files (wwwroot)
        app.UseStaticFiles();

        app.MapControllers();
        app.MapRazorPages();
        app.MapAssistantSpeechEndpoints();
        app.MapTtsEndpoints();
        app.MapMuteEndpoints();
        app.MapDesktopMonitorHub();
        app.MapGet("/health", () => Results.Ok("OK"));

        return app;
    }

    /// <summary>
    /// Maps assistant speech tracking endpoints (for echo cancellation).
    /// Dependencies are injected directly into handlers (issue #691 - explicit dependencies).
    /// </summary>
    public static WebApplication MapAssistantSpeechEndpoints(this WebApplication app)
    {
        app.MapPost("/api/assistant-speech/start", (
            AssistantSpeechStartRequest request,
            IAssistantSpeechTrackerService speechTracker,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("TTS Start: \"{Text}\"", request.Text);
            speechTracker.StartSpeaking(request.Text);
            return Results.Ok(new { status = "started", text = request.Text });
        });

        app.MapPost("/api/assistant-speech/end", (IAssistantSpeechTrackerService speechTracker) =>
        {
            speechTracker.StopSpeaking();
            return Results.Ok(new { status = "ended" });
        });

        app.MapGet("/api/assistant-speech/status", (IAssistantSpeechTrackerService speechTracker) =>
        {
            return Results.Ok(new { historyCount = speechTracker.GetHistoryCount() });
        });

        return app;
    }

    /// <summary>
    /// Maps TTS endpoints (speak, queue management, provider status).
    /// All TTS operations delegate to TtsService (single source of truth).
    /// Note: Notifications are handled by NotificationsController at /api/notifications
    /// Dependencies are injected directly into handlers (issue #691 - explicit dependencies).
    /// </summary>
    public static WebApplication MapTtsEndpoints(this WebApplication app)
    {
        // Speak endpoint - directly speaks text via TTS
        app.MapPost("/api/tts/speak", async (
            TtsNotifyRequest request,
            TtsService ttsService,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("TTS Speak received from {Source}: {Text}", request.Source ?? "default", request.Text);

            // Fire and forget - TtsService handles queueing, caching, playback
            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, providerUsed) = await ttsService.SpeakAsync(request.Text, request.Source);
                    logger.LogDebug("TTS completed: success={Success}, provider={Provider}", success, providerUsed);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TTS processing failed");
                }
            });

            return Results.Ok(new { success = true, message = "TTS processing", text = request.Text, source = request.Source ?? "NOT_SET" });
        });

        // Provider status
        app.MapGet("/api/tts/providers", (ITtsProviderChain ttsProviderChain) =>
        {
            var statuses = ttsProviderChain.GetProvidersStatus();
            return Results.Ok(new { providers = statuses });
        });

        // Circuit breaker reset
        app.MapPost("/api/tts/reset-circuit-breaker", (string? provider, ITtsProviderChain ttsProviderChain) =>
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
            logger.LogInformation("TTS Stop requested - stopping playback");
            ttsService.StopPlayback();
            return Results.Ok(new { success = true, message = "Playback stopped" });
        });

        // Flush queue
        app.MapPost("/api/tts/flush-queue", async (TtsService ttsService, ILogger<Program> logger) =>
        {
            var queueCount = ttsService.QueueCount;
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
    /// Dependencies are injected directly into handlers (issue #691 - explicit dependencies).
    /// </summary>
    public static WebApplication MapMuteEndpoints(this WebApplication app)
    {
        app.MapPost("/api/mute", (
            MuteRequest request,
            IManualMuteService muteService,
            ILogger<Program> logger) =>
        {
            var previousState = muteService.IsMuted;
            muteService.SetMuted(request.Muted);

            logger.LogInformation("Mute state changed via API: {PreviousState} -> {NewState}", previousState, request.Muted);

            return Results.Ok(new { success = true, muted = muteService.IsMuted, previousState });
        });

        app.MapGet("/api/mute", (IManualMuteService muteService) =>
        {
            return Results.Ok(new { muted = muteService.IsMuted });
        });

        return app;
    }

    /// <summary>
    /// Maps Desktop Monitor SignalR hub endpoint.
    /// WARNING: No authentication configured. Desktop context data (workspace, window titles, app names)
    /// is broadcast to all connected clients. Intended for localhost use only.
    /// For network access, implement authentication/authorization.
    /// </summary>
    public static WebApplication MapDesktopMonitorHub(this WebApplication app)
    {
        app.MapHub<DesktopMonitorHub>("/hub/desktop-monitor");
        return app;
    }
}
