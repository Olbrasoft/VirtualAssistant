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
    /// Gets or sets the AgentMessages DbSet for inter-agent communication.
    /// </summary>
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();

    /// <summary>
    /// Gets or sets the AgentMessageLogs DbSet for hub message logging.
    /// </summary>
    public DbSet<AgentMessageLog> AgentMessageLogs => Set<AgentMessageLog>();

    /// <summary>
    /// Gets or sets the Agents DbSet for registered agent workers.
    /// </summary>
    public DbSet<Agent> Agents => Set<Agent>();

    /// <summary>
    /// Gets or sets the AgentTasks DbSet for inter-agent task queue.
    /// </summary>
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();

    /// <summary>
    /// Gets or sets the AgentTaskSends DbSet for task delivery logs.
    /// </summary>
    public DbSet<AgentTaskSend> AgentTaskSends => Set<AgentTaskSend>();

    /// <summary>
    /// Gets or sets the AgentResponses DbSet for simplified agent status tracking.
    /// One record per agent work session (monolog).
    /// </summary>
    public DbSet<AgentResponse> AgentResponses => Set<AgentResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VirtualAssistantDbContext).Assembly);
    }
}
