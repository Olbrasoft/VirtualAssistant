using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Minimal reference to a GitHub repository.
/// URL can be constructed as: https://github.com/{Owner}/{Name}
/// </summary>
public class GitHubRepository : BaseEnity
{
    /// <summary>
    /// Gets or sets the repository owner (username or organization).
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the repository name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of issues in this repository.
    /// </summary>
    public ICollection<GitHubIssue> Issues { get; set; } = [];

    /// <summary>
    /// Constructs the GitHub URL for this repository.
    /// </summary>
    public string GetUrl() => $"https://github.com/{Owner}/{Name}";
}
