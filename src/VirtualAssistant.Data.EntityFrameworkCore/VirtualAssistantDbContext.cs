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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VirtualAssistantDbContext).Assembly);
    }
}
