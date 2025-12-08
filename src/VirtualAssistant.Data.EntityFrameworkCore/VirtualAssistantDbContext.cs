using Pgvector.EntityFrameworkCore;

namespace VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Database context for VirtualAssistant using PostgreSQL.
/// </summary>
public class VirtualAssistantDbContext : DbContext
{
    private readonly bool _isInMemory;

    public VirtualAssistantDbContext(DbContextOptions<VirtualAssistantDbContext> options) : base(options)
    {
        // Detect InMemory database from options extensions (more reliable than Database.ProviderName)
        _isInMemory = options.Extensions.Any(e => e.GetType().Name.Contains("InMemory"));
    }

    /// <summary>
    /// Gets or sets the Conversations DbSet.
    /// </summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>
    /// Gets or sets the Messages DbSet.
    /// </summary>
    public DbSet<Message> Messages => Set<Message>();

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
    /// Gets or sets the GitHubIssueAgents DbSet for many-to-many relationship.
    /// </summary>
    public DbSet<GitHubIssueAgent> GitHubIssueAgents => Set<GitHubIssueAgent>();

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

        if (!_isInMemory)
        {
            // Enable pgvector extension (only for PostgreSQL)
            modelBuilder.HasPostgresExtension("vector");
        }

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VirtualAssistantDbContext).Assembly);

        if (_isInMemory)
        {
            // For InMemory testing, ignore vector properties AFTER configurations are applied
            modelBuilder.Entity<GitHubIssue>().Ignore(e => e.TitleEmbedding);
            modelBuilder.Entity<GitHubIssue>().Ignore(e => e.BodyEmbedding);
        }
    }
}
