namespace Olbrasoft.VirtualAssistant.Data.Commands.GitHubCommands;

/// <summary>
/// Command to ensure a GitHub repository exists in the database.
/// Returns existing or newly created repository ID.
/// </summary>
/// <param name="Owner">Repository owner (username or organization).</param>
/// <param name="Name">Repository name.</param>
public record EnsureRepositoryExistsCommand(
    string Owner,
    string Name
) : ICommand<int>;
