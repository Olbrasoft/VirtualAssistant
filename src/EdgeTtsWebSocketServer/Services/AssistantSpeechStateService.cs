using Microsoft.EntityFrameworkCore;
using Olbrasoft.VoiceAssistant.Data.EntityFrameworkCore;
using Olbrasoft.VoiceAssistant.Shared.Data.Entities;

namespace Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Services;

/// <summary>
/// Service for tracking assistant speech state in the database.
/// Used to prevent locking TTS when the listener hears the assistant's own voice.
/// </summary>
public class AssistantSpeechStateService
{
    private readonly ILogger<AssistantSpeechStateService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private const int StateRecordId = 1; // Singleton record

    public AssistantSpeechStateService(
        ILogger<AssistantSpeechStateService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Checks if the assistant is currently speaking.
    /// </summary>
    /// <returns>True if assistant is speaking, false otherwise.</returns>
    public async Task<bool> IsAssistantSpeakingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VoiceAssistantDbContext>();

            var state = await dbContext.AssistantSpeechStates
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == StateRecordId, cancellationToken);

            return state?.IsSpeaking ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check assistant speech state, assuming not speaking");
            return false;
        }
    }

    /// <summary>
    /// Marks the assistant as speaking (called before TTS playback).
    /// </summary>
    public async Task StartSpeakingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VoiceAssistantDbContext>();

            var state = await dbContext.AssistantSpeechStates
                .FirstOrDefaultAsync(s => s.Id == StateRecordId, cancellationToken);

            if (state == null)
            {
                // Create if doesn't exist
                state = new AssistantSpeechState
                {
                    Id = StateRecordId,
                    IsSpeaking = true,
                    StartedAt = DateTime.UtcNow,
                    EndedAt = null,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.AssistantSpeechStates.Add(state);
            }
            else
            {
                state.IsSpeaking = true;
                state.StartedAt = DateTime.UtcNow;
                state.EndedAt = null;
                state.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Assistant started speaking");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark assistant as speaking");
        }
    }

    /// <summary>
    /// Marks the assistant as not speaking (called after TTS playback).
    /// </summary>
    public async Task StopSpeakingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VoiceAssistantDbContext>();

            var state = await dbContext.AssistantSpeechStates
                .FirstOrDefaultAsync(s => s.Id == StateRecordId, cancellationToken);

            if (state != null)
            {
                state.IsSpeaking = false;
                state.EndedAt = DateTime.UtcNow;
                state.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogDebug("Assistant stopped speaking");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark assistant as not speaking");
        }
    }
}
