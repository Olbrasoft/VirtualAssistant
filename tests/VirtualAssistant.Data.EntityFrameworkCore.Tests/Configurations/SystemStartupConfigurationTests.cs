using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.Configurations;

public class SystemStartupConfigurationTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task SystemStartup_CanBeSavedAndRetrieved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var startup = new SystemStartup
        {
            StartedAt = DateTime.UtcNow,
            StartupType = StartupType.Normal,
            GreetingSpoken = "Dobrý den!"
        };

        // Act
        context.SystemStartups.Add(startup);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.SystemStartups.FirstAsync();
        Assert.Equal(StartupType.Normal, saved.StartupType);
        Assert.Equal("Dobrý den!", saved.GreetingSpoken);
    }

    [Fact]
    public async Task SystemStartup_WithShutdown_CanBeSavedAndRetrieved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var startTime = DateTime.UtcNow;
        var shutdownTime = startTime.AddHours(8);

        var startup = new SystemStartup
        {
            StartedAt = startTime,
            ShutdownAt = shutdownTime,
            ShutdownType = ShutdownType.Clean,
            StartupType = StartupType.Normal
        };

        // Act
        context.SystemStartups.Add(startup);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.SystemStartups.FirstAsync();
        Assert.Equal(shutdownTime, saved.ShutdownAt);
        Assert.Equal(ShutdownType.Clean, saved.ShutdownType);
    }

    [Fact]
    public async Task SystemStartup_AllStartupTypes_CanBeSaved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var startupTypes = Enum.GetValues<StartupType>();

        // Act
        foreach (var startupType in startupTypes)
        {
            context.SystemStartups.Add(new SystemStartup
            {
                StartedAt = DateTime.UtcNow,
                StartupType = startupType
            });
        }
        await context.SaveChangesAsync();

        // Assert
        var count = await context.SystemStartups.CountAsync();
        Assert.Equal(startupTypes.Length, count);
    }

    [Fact]
    public async Task SystemStartup_AllShutdownTypes_CanBeSaved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var shutdownTypes = Enum.GetValues<ShutdownType>();

        // Act
        foreach (var shutdownType in shutdownTypes)
        {
            context.SystemStartups.Add(new SystemStartup
            {
                StartedAt = DateTime.UtcNow,
                ShutdownAt = DateTime.UtcNow.AddHours(1),
                ShutdownType = shutdownType,
                StartupType = StartupType.Normal
            });
        }
        await context.SaveChangesAsync();

        // Assert
        var count = await context.SystemStartups.CountAsync();
        Assert.Equal(shutdownTypes.Length, count);
    }

    [Fact]
    public async Task SystemStartup_AutoGeneratesId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var startup = new SystemStartup
        {
            StartedAt = DateTime.UtcNow,
            StartupType = StartupType.Normal
        };

        // Act
        context.SystemStartups.Add(startup);
        await context.SaveChangesAsync();

        // Assert
        Assert.True(startup.Id > 0);
    }

    [Fact]
    public async Task SystemStartup_CanQueryByStartedAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var cutoffTime = DateTime.UtcNow;

        context.SystemStartups.AddRange(
            new SystemStartup { StartedAt = cutoffTime.AddDays(-7), StartupType = StartupType.Normal },
            new SystemStartup { StartedAt = cutoffTime.AddDays(-1), StartupType = StartupType.AfterCrash },
            new SystemStartup { StartedAt = cutoffTime.AddHours(1), StartupType = StartupType.Normal }
        );
        await context.SaveChangesAsync();

        // Act
        var recentStartups = await context.SystemStartups
            .Where(s => s.StartedAt > cutoffTime)
            .ToListAsync();

        // Assert
        Assert.Single(recentStartups);
    }

    [Fact]
    public async Task SystemStartup_CanFindUnfinishedSessions()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.SystemStartups.AddRange(
            new SystemStartup
            {
                StartedAt = DateTime.UtcNow.AddHours(-10),
                ShutdownAt = DateTime.UtcNow.AddHours(-2),
                ShutdownType = ShutdownType.Clean,
                StartupType = StartupType.Normal
            },
            new SystemStartup
            {
                StartedAt = DateTime.UtcNow.AddHours(-1),
                ShutdownAt = null,
                ShutdownType = null,
                StartupType = StartupType.Normal
            }
        );
        await context.SaveChangesAsync();

        // Act
        var unfinishedSessions = await context.SystemStartups
            .Where(s => s.ShutdownAt == null)
            .ToListAsync();

        // Assert
        Assert.Single(unfinishedSessions);
    }

    [Fact]
    public async Task SystemStartup_CanUpdateShutdownInfo()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var startup = new SystemStartup
        {
            StartedAt = DateTime.UtcNow,
            StartupType = StartupType.Normal
        };
        context.SystemStartups.Add(startup);
        await context.SaveChangesAsync();

        // Act - simulate clean shutdown
        startup.ShutdownAt = DateTime.UtcNow.AddHours(8);
        startup.ShutdownType = ShutdownType.Clean;
        await context.SaveChangesAsync();

        // Assert
        var updated = await context.SystemStartups.FirstAsync(s => s.Id == startup.Id);
        Assert.NotNull(updated.ShutdownAt);
        Assert.Equal(ShutdownType.Clean, updated.ShutdownType);
    }

    [Fact]
    public async Task SystemStartup_LongGreeting_CanBeSaved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var longGreeting = new string('x', 1000); // 1000 characters
        var startup = new SystemStartup
        {
            StartedAt = DateTime.UtcNow,
            StartupType = StartupType.Normal,
            GreetingSpoken = longGreeting
        };

        // Act
        context.SystemStartups.Add(startup);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.SystemStartups.FirstAsync();
        Assert.Equal(1000, saved.GreetingSpoken!.Length);
    }
}
