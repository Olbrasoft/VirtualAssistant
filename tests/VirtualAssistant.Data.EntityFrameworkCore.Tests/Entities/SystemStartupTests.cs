using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.Entities;

public class SystemStartupTests
{
    [Fact]
    public void Constructor_SetsDefaultStartedAt()
    {
        // Arrange & Act
        var beforeCreation = DateTime.UtcNow;
        var startup = new SystemStartup();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(startup.StartedAt >= beforeCreation);
        Assert.True(startup.StartedAt <= afterCreation);
    }

    [Fact]
    public void ShutdownAt_IsNullByDefault()
    {
        // Arrange
        var startup = new SystemStartup();

        // Assert
        Assert.Null(startup.ShutdownAt);
    }

    [Fact]
    public void ShutdownAt_CanBeSet()
    {
        // Arrange
        var shutdownTime = DateTime.UtcNow.AddHours(1);
        var startup = new SystemStartup { ShutdownAt = shutdownTime };

        // Assert
        Assert.Equal(shutdownTime, startup.ShutdownAt);
    }

    [Fact]
    public void ShutdownType_IsNullByDefault()
    {
        // Arrange
        var startup = new SystemStartup();

        // Assert
        Assert.Null(startup.ShutdownType);
    }

    [Fact]
    public void ShutdownType_CanBeSetToClean()
    {
        // Arrange
        var startup = new SystemStartup { ShutdownType = ShutdownType.Clean };

        // Assert
        Assert.Equal(ShutdownType.Clean, startup.ShutdownType);
    }

    [Fact]
    public void ShutdownType_CanBeSetToCrash()
    {
        // Arrange
        var startup = new SystemStartup { ShutdownType = ShutdownType.Crash };

        // Assert
        Assert.Equal(ShutdownType.Crash, startup.ShutdownType);
    }

    [Fact]
    public void StartupType_DefaultsToNormal()
    {
        // Arrange
        var startup = new SystemStartup();

        // Assert
        Assert.Equal(StartupType.Normal, startup.StartupType);
    }

    [Theory]
    [InlineData(StartupType.Normal)]
    [InlineData(StartupType.AfterCrash)]
    [InlineData(StartupType.FrequentRestart)]
    [InlineData(StartupType.Development)]
    public void StartupType_CanBeSetToAllValues(StartupType startupType)
    {
        // Arrange
        var startup = new SystemStartup { StartupType = startupType };

        // Assert
        Assert.Equal(startupType, startup.StartupType);
    }

    [Fact]
    public void GreetingSpoken_IsNullByDefault()
    {
        // Arrange
        var startup = new SystemStartup();

        // Assert
        Assert.Null(startup.GreetingSpoken);
    }

    [Fact]
    public void GreetingSpoken_CanBeSet()
    {
        // Arrange
        var startup = new SystemStartup { GreetingSpoken = "Dobrý den, Jirko!" };

        // Assert
        Assert.Equal("Dobrý den, Jirko!", startup.GreetingSpoken);
    }

    [Fact]
    public void Id_InheritsFromBaseEntity()
    {
        // Arrange
        var startup = new SystemStartup();

        // Assert - Id should be default (0) for new entity
        Assert.Equal(0, startup.Id);
    }

    [Fact]
    public void SimulateCleanShutdown_SetsShutdownAtAndType()
    {
        // Arrange
        var startup = new SystemStartup { StartupType = StartupType.Normal };
        var beforeShutdown = DateTime.UtcNow;

        // Act - simulate clean shutdown
        startup.ShutdownAt = DateTime.UtcNow;
        startup.ShutdownType = ShutdownType.Clean;
        var afterShutdown = DateTime.UtcNow;

        // Assert
        Assert.NotNull(startup.ShutdownAt);
        Assert.True(startup.ShutdownAt >= beforeShutdown);
        Assert.True(startup.ShutdownAt <= afterShutdown);
        Assert.Equal(ShutdownType.Clean, startup.ShutdownType);
    }

    [Fact]
    public void SimulateCrashDetection_PreviousSessionWithoutShutdown()
    {
        // Arrange - previous session without clean shutdown
        var previousSession = new SystemStartup
        {
            StartedAt = DateTime.UtcNow.AddHours(-2),
            ShutdownAt = null,
            ShutdownType = null
        };

        // Act - detect crash based on missing shutdown
        var isCrash = previousSession.ShutdownAt == null;

        // Assert
        Assert.True(isCrash);
    }
}
