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
    public void DefaultValues_AreEmptyStrings()
    {
        // Arrange
        var settings = new GitHubSettings();

        // Assert
        Assert.Equal(string.Empty, settings.Token);
        Assert.Equal(string.Empty, settings.Owner);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var settings = new GitHubSettings
        {
            Token = "ghp_test123",
            Owner = "TestOwner"
        };

        // Assert
        Assert.Equal("ghp_test123", settings.Token);
        Assert.Equal("TestOwner", settings.Owner);
    }
}
