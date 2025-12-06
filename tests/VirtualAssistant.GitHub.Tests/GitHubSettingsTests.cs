using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Tests;

public class GitHubSettingsTests
{
    [Fact]
    public void SectionName_ReturnsGitHub()
    {
        // Assert
        Assert.Equal("GitHub", GitHubSettings.SectionName);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var settings = new GitHubSettings();

        // Assert
        Assert.Equal(string.Empty, settings.Token);
        Assert.Equal(string.Empty, settings.Owner);
        Assert.True(settings.EnableScheduledSync);
        Assert.Equal(60, settings.SyncIntervalMinutes);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var settings = new GitHubSettings
        {
            Token = "ghp_test123",
            Owner = "TestOwner",
            EnableScheduledSync = false,
            SyncIntervalMinutes = 30
        };

        // Assert
        Assert.Equal("ghp_test123", settings.Token);
        Assert.Equal("TestOwner", settings.Owner);
        Assert.False(settings.EnableScheduledSync);
        Assert.Equal(30, settings.SyncIntervalMinutes);
    }
}
