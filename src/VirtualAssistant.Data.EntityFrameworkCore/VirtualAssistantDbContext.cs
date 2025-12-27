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

    /// <summary>
    /// Gets or sets the WhisperTranscriptions DbSet.
    /// </summary>
    public DbSet<WhisperTranscription> WhisperTranscriptions => Set<WhisperTranscription>();

    /// <summary>
    /// Gets or sets the LlmCorrections DbSet.
    /// </summary>
    public DbSet<LlmCorrection> LlmCorrections => Set<LlmCorrection>();

    /// <summary>
    /// Gets or sets the LlmErrors DbSet.
    /// </summary>
    public DbSet<LlmError> LlmErrors => Set<LlmError>();

    /// <summary>
    /// Gets or sets the TranscriptionCorrections DbSet.
    /// </summary>
    public DbSet<TranscriptionCorrection> TranscriptionCorrections => Set<TranscriptionCorrection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VirtualAssistantDbContext).Assembly);
    }
}
