using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Commands.GitHubCommands;

/// <summary>
/// Command to ensure a GitHub issue exists in the database.
/// Creates repository if it doesn't exist.
/// Returns existing or newly created issue ID.
/// </summary>
/// <param name="Owner">Repository owner.</param>
/// <param name="Name">Repository name.</param>
/// <param name="IssueNumber">Issue number within the repository.</param>
public record EnsureIssueExistsCommand(
    string Owner,
    string Name,
    int IssueNumber
) : ICommand<int>;
