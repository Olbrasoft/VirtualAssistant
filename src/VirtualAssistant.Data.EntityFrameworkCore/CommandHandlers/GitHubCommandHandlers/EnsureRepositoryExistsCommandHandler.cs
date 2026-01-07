using Olbrasoft.VirtualAssistant.Data.Commands.GitHubCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.GitHubCommandHandlers;

/// <summary>
/// Handler for EnsureRepositoryExistsCommand.
/// Ensures a GitHub repository exists, creating it if necessary.
/// </summary>
public class EnsureRepositoryExistsCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<EnsureRepositoryExistsCommand, GitHubRepository, int>(context)
{
    protected override async Task<int> GetResultToHandleAsync(EnsureRepositoryExistsCommand command, CancellationToken token)
    {
        var repo = await Context.GitHubRepositories
            .FirstOrDefaultAsync(r => r.Owner == command.Owner && r.Name == command.Name, token);

        if (repo != null)
            return repo.Id;

        repo = new GitHubRepository { Owner = command.Owner, Name = command.Name };
        Context.GitHubRepositories.Add(repo);
        await Context.SaveChangesAsync(token);

        return repo.Id;
    }
}
