using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.NotificationCommandHandlers;

/// <summary>
/// Handler for RecordTtsOutcomeCommand.
/// Records the final TTS outcome for a notification.
/// </summary>
public class RecordTtsOutcomeCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<RecordTtsOutcomeCommand, Notification>(context)
{
    protected override async Task<bool> GetResultToHandleAsync(RecordTtsOutcomeCommand command, CancellationToken token)
    {
        var notification = await Context.Notifications.FindAsync([command.NotificationId], token);
        if (notification == null)
        {
            return false;
        }

        Provider? provider = null;
        if (command.ProviderName != null)
        {
            provider = await Context.Providers
                .FirstOrDefaultAsync(p => p.Name == command.ProviderName && p.Type == "tts", token);

            if (provider == null)
            {
                provider = new Provider
                {
                    Name = command.ProviderName,
                    Type = "tts",
                    Enabled = true,
                    Priority = 0
                };
                Context.Providers.Add(provider);
            }
        }

        // Use navigation property - EF Core handles FK assignment
        notification.FinalProvider = provider;
        notification.FinalTtsStatus = command.Status;
        notification.TtsCompletedAt = DateTime.UtcNow;

        if (provider != null)
        {
            var attempt = new NotificationTtsAttempt
            {
                NotificationId = command.NotificationId,
                Provider = provider,  // Use navigation property instead of ID
                AttemptOrder = 1,
                StatusCode = command.Status,
                DurationMs = command.DurationMs,
                CreatedAt = DateTime.UtcNow
            };
            Context.NotificationTtsAttempts.Add(attempt);
        }

        // Single SaveChanges - handles both provider creation and notification update
        try
        {
            await Context.SaveChangesAsync(token);
        }
        catch (DbUpdateException) when (provider != null && Context.Entry(provider).State == EntityState.Added)
        {
            // Race condition: another request created the same provider
            // Detach and retry with existing provider
            Context.Entry(provider).State = EntityState.Detached;

            provider = await Context.Providers
                .FirstOrDefaultAsync(p => p.Name == command.ProviderName && p.Type == "tts", token);

            if (provider == null)
                throw;

            notification.FinalProvider = provider;
            notification.FinalProviderId = provider.Id;

            // Recreate attempt with existing provider
            var existingAttempt = Context.ChangeTracker.Entries<NotificationTtsAttempt>()
                .FirstOrDefault(e => e.Entity.NotificationId == command.NotificationId);
            if (existingAttempt != null)
            {
                existingAttempt.State = EntityState.Detached;
            }

            var retryAttempt = new NotificationTtsAttempt
            {
                NotificationId = command.NotificationId,
                Provider = provider,
                AttemptOrder = 1,
                StatusCode = command.Status,
                DurationMs = command.DurationMs,
                CreatedAt = DateTime.UtcNow
            };
            Context.NotificationTtsAttempts.Add(retryAttempt);

            await Context.SaveChangesAsync(token);
        }

        return true;
    }
}
