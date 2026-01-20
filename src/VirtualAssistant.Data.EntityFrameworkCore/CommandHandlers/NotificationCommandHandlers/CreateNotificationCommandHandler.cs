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
    /// </summary>
    private async Task<(int? ProviderId, int? ModelId)> GetOrCreateLlmInfoAsync(
        string? providerName, string? modelName, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(providerName) && string.IsNullOrWhiteSpace(modelName))
        {
            return (null, null);
        }

        int? providerId = null;
        int? modelId = null;

        // Get or create LLM provider
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            var provider = await Context.Providers
                .FirstOrDefaultAsync(p => p.Name == providerName && p.Type == "llm", token);

            if (provider == null)
            {
                provider = new Provider
                {
                    Name = providerName,
                    Type = "llm",
                    Enabled = true,
                    Priority = 0,
                    CreatedAt = DateTime.UtcNow
                };
                Context.Providers.Add(provider);
                await Context.SaveChangesAsync(token);
            }

            providerId = provider.Id;
        }

        // Get or create LLM model
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var model = await Context.LlmModels
                .FirstOrDefaultAsync(m => m.ModelIdentifier == modelName, token);

            if (model == null)
            {
                // Create new model (provider relationship via ModelProviderMapping)
                model = new LlmModel
                {
                    Name = modelName,
                    ModelIdentifier = modelName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                Context.LlmModels.Add(model);
                await Context.SaveChangesAsync(token);
            }

            modelId = model.Id;

            // Ensure mapping exists between model and provider
            if (providerId.HasValue && modelId.HasValue)
            {
                var mappingExists = await Context.ModelProviderMappings
                    .AnyAsync(m => m.ModelId == modelId && m.ProviderId == providerId, token);

                if (!mappingExists)
                {
                    Context.ModelProviderMappings.Add(new ModelProviderMapping
                    {
                        ModelId = modelId.Value,
                        ProviderId = providerId.Value,
                        CreatedAt = DateTime.UtcNow
                    });
                    await Context.SaveChangesAsync(token);
                }
            }
        }

        return (providerId, modelId);
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
