using Olbrasoft.VirtualAssistant.Data.Commands.GitHubCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.GitHubCommandHandlers;

/// <summary>
/// Handler for EnsureIssueExistsCommand.
/// Ensures a GitHub issue exists, creating repository and issue if necessary.
/// </summary>
public class EnsureIssueExistsCommandHandler(VirtualAssistantDbContext context, ICommandExecutor commandExecutor)
    : VirtualAssistantDbCommandHandler<EnsureIssueExistsCommand, GitHubIssue, int>(context)
{
    protected override async Task<int> GetResultToHandleAsync(EnsureIssueExistsCommand command, CancellationToken token)
    {
        // Ensure repository exists first
        var repoCommand = new EnsureRepositoryExistsCommand(command.Owner, command.Name);
        var repoId = await commandExecutor.ExecuteAsync(repoCommand, token);

        var issue = await Context.GitHubIssues
            .FirstOrDefaultAsync(i => i.RepositoryId == repoId && i.IssueNumber == command.IssueNumber, token);

        if (issue != null)
            return issue.Id;

        issue = new GitHubIssue { RepositoryId = repoId, IssueNumber = command.IssueNumber };
        Context.GitHubIssues.Add(issue);
        await Context.SaveChangesAsync(token);

        return issue.Id;
    }
}
