namespace VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Database context for VirtualAssistant using PostgreSQL.
/// </summary>
public class VirtualAssistantDbContext : DbContext
{
    public VirtualAssistantDbContext(DbContextOptions<VirtualAssistantDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the VoiceTranscriptions DbSet.
    /// </summary>
    public DbSet<VoiceTranscription> VoiceTranscriptions => Set<VoiceTranscription>();

    /// <summary>
    /// Gets or sets the SystemStartups DbSet.
    /// </summary>
    public DbSet<SystemStartup> SystemStartups => Set<SystemStartup>();

    /// <summary>
    /// Gets or sets the GitHubRepositories DbSet.
    /// </summary>
    public DbSet<GitHubRepository> GitHubRepositories => Set<GitHubRepository>();

    /// <summary>
    /// Gets or sets the GitHubIssues DbSet.
    /// </summary>
    public DbSet<GitHubIssue> GitHubIssues => Set<GitHubIssue>();

    /// <summary>
    /// Gets or sets the Agents DbSet for registered agent workers.
    /// </summary>
    public DbSet<Agent> Agents => Set<Agent>();

    /// <summary>
    /// Gets or sets the NotificationStatuses DbSet (reference table).
    /// </summary>
    public DbSet<NotificationStatus> NotificationStatuses => Set<NotificationStatus>();

    /// <summary>
    /// Gets or sets the Notifications DbSet for agent notifications.
    /// </summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>
    /// Gets or sets the NotificationGitHubIssues DbSet (junction table).
    /// </summary>
    public DbSet<NotificationGitHubIssue> NotificationGitHubIssues => Set<NotificationGitHubIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VirtualAssistantDbContext).Assembly);
    }
}
