using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;
using Olbrasoft.VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.NotificationCommandHandlers;

/// <summary>
/// Handler for CreateNotificationCommand.
/// Creates a new notification in the database with optional LLM tracking.
/// </summary>
public class CreateNotificationCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<CreateNotificationCommand, Notification, int>(context)
{
    protected override async Task<int> GetResultToHandleAsync(CreateNotificationCommand command, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Text, nameof(command.Text));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.AgentName, nameof(command.AgentName));

        var agentType = MapAgentNameToType(command.AgentName);

        // Get or create LLM provider and model if provided
        var (llmProviderId, llmModelId) = await GetOrCreateLlmInfoAsync(
            command.ProviderName, command.ModelName, token);

        var notification = new Notification
        {
            Text = command.Text,
            AgentId = (int)agentType,
            CreatedAt = DateTime.UtcNow,
            NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived,
            LlmProviderId = llmProviderId,
            LlmModelId = llmModelId
        };

        Context.Notifications.Add(notification);

        // Add issue links using navigation property - EF Core handles FK assignment
        if (command.IssueIds is { Count: > 0 })
        {
            foreach (var issueId in command.IssueIds.Distinct())
            {
                Context.NotificationGitHubIssues.Add(new NotificationGitHubIssue
                {
                    Notification = notification,  // Use navigation property instead of ID
                    GitHubIssueId = issueId
                });
            }
        }

        // Single SaveChanges - EF Core sets notification.Id and propagates to NotificationGitHubIssue.NotificationId
        await Context.SaveChangesAsync(token);

        return notification.Id;
    }

    /// <summary>
    /// Gets or creates LLM provider and model, ensuring the mapping exists between them.
    /// Uses transaction to ensure atomicity and handles race conditions gracefully.
    /// </summary>
    /// <remarks>
    /// Provider and model can be specified independently:
    /// - Only providerName: Creates/finds provider, notification linked to provider only
    /// - Only modelName: Creates/finds model, notification linked to model only (no provider)
    /// - Both: Creates/finds both and ensures mapping exists between them
    /// </remarks>
    private async Task<(int? ProviderId, int? ModelId)> GetOrCreateLlmInfoAsync(
        string? providerName, string? modelName, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(providerName) && string.IsNullOrWhiteSpace(modelName))
        {
            return (null, null);
        }

        await using var transaction = await Context.Database.BeginTransactionAsync(token);
        try
        {
            int? providerId = null;
            int? modelId = null;

            // Get or create LLM provider
            if (!string.IsNullOrWhiteSpace(providerName))
            {
                providerId = await GetOrCreateProviderAsync(providerName, token);
            }

            // Get or create LLM model
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                modelId = await GetOrCreateModelAsync(modelName, token);

                // Ensure mapping exists between model and provider
                if (providerId.HasValue && modelId.HasValue)
                {
                    await EnsureMappingExistsAsync(modelId.Value, providerId.Value, token);
                }
            }

            await transaction.CommitAsync(token);
            return (providerId, modelId);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }

    private async Task<int> GetOrCreateProviderAsync(string providerName, CancellationToken token)
    {
        var provider = await Context.Providers
            .FirstOrDefaultAsync(p => p.Name == providerName && p.Type == "llm", token);

        if (provider != null)
        {
            return provider.Id;
        }

        // Try to create, handle race condition if another request created it
        provider = new Provider
        {
            Name = providerName,
            Type = "llm",
            Enabled = true,
            Priority = 0,
            CreatedAt = DateTime.UtcNow
        };

        Context.Providers.Add(provider);

        try
        {
            await Context.SaveChangesAsync(token);
            return provider.Id;
        }
        catch (DbUpdateException)
        {
            // Race condition: another request created the provider, fetch it
            Context.Entry(provider).State = EntityState.Detached;
            var existingProvider = await Context.Providers
                .FirstAsync(p => p.Name == providerName && p.Type == "llm", token);
            return existingProvider.Id;
        }
    }

    private async Task<int> GetOrCreateModelAsync(string modelName, CancellationToken token)
    {
        var model = await Context.LlmModels
            .FirstOrDefaultAsync(m => m.ModelIdentifier == modelName, token);

        if (model != null)
        {
            return model.Id;
        }

        // Try to create, handle race condition if another request created it
        model = new LlmModel
        {
            Name = modelName,
            ModelIdentifier = modelName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        Context.LlmModels.Add(model);

        try
        {
            await Context.SaveChangesAsync(token);
            return model.Id;
        }
        catch (DbUpdateException)
        {
            // Race condition: another request created the model, fetch it
            Context.Entry(model).State = EntityState.Detached;
            var existingModel = await Context.LlmModels
                .FirstAsync(m => m.ModelIdentifier == modelName, token);
            return existingModel.Id;
        }
    }

    private async Task EnsureMappingExistsAsync(int modelId, int providerId, CancellationToken token)
    {
        var mappingExists = await Context.ModelProviderMappings
            .AnyAsync(m => m.ModelId == modelId && m.ProviderId == providerId, token);

        if (mappingExists)
        {
            return;
        }

        // Try to create, handle race condition if another request created it
        var mapping = new ModelProviderMapping
        {
            ModelId = modelId,
            ProviderId = providerId,
            CreatedAt = DateTime.UtcNow
        };

        Context.ModelProviderMappings.Add(mapping);

        try
        {
            await Context.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            // Race condition: another request created the mapping, ignore
            Context.Entry(mapping).State = EntityState.Detached;
        }
    }

    private static AgentType MapAgentNameToType(string agentName)
    {
        var normalized = agentName.ToLowerInvariant().Trim();

        return normalized switch
        {
            "opencode" => AgentType.OpenCode,
            "claude" or "claude-code" => AgentType.ClaudeCode,
            "gemini" => AgentType.Gemini,
            "antigravity" => AgentType.Antigravity,
            _ => throw new ArgumentException(
                $"Invalid agent name '{agentName}'. Allowed values: opencode, claude, claude-code, gemini, antigravity",
                nameof(agentName))
        };
    }
}
