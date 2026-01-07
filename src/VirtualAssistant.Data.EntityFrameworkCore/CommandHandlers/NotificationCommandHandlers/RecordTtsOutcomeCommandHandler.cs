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
                try
                {
                    provider = new Provider
                    {
                        Name = command.ProviderName,
                        Type = "tts",
                        Enabled = true,
                        Priority = 0
                    };
                    Context.Providers.Add(provider);
                    await Context.SaveChangesAsync(token);
                }
                catch (DbUpdateException)
                {
                    Context.Entry(provider!).State = EntityState.Detached;
                    provider = await Context.Providers
                        .FirstOrDefaultAsync(p => p.Name == command.ProviderName && p.Type == "tts", token);

                    if (provider == null)
                        throw;
                }
            }
        }

        notification.FinalProviderId = provider?.Id;
        notification.FinalTtsStatus = command.Status;
        notification.TtsCompletedAt = DateTime.UtcNow;

        if (provider != null)
        {
            var attempt = new NotificationTtsAttempt
            {
                NotificationId = command.NotificationId,
                ProviderId = provider.Id,
                AttemptOrder = 1,
                StatusCode = command.Status,
                DurationMs = command.DurationMs,
                CreatedAt = DateTime.UtcNow
            };
            Context.NotificationTtsAttempts.Add(attempt);
        }

        await Context.SaveChangesAsync(token);
        return true;
    }
}
