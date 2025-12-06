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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VirtualAssistantDbContext).Assembly);
    }
}
