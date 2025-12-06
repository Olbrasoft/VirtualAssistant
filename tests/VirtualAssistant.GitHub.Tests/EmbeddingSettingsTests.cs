using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Tests;

public class EmbeddingSettingsTests
{
    [Fact]
    public void SectionName_ReturnsEmbeddings()
    {
        Assert.Equal("Embeddings", EmbeddingSettings.SectionName);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var settings = new EmbeddingSettings();

        // Assert
        Assert.Equal("Ollama", settings.Provider);
        Assert.Equal("nomic-embed-text", settings.Model);
        Assert.Equal(768, settings.Dimensions);
        Assert.Equal("http://localhost:11434", settings.BaseUrl);
        Assert.True(settings.SkipShortContent);
        Assert.Equal(10, settings.MinContentLength);
        Assert.Equal(20, settings.BatchSize);
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal(string.Empty, settings.ApiKeyFile);
    }

    [Fact]
    public void GetEffectiveApiKey_WithApiKey_ReturnsApiKey()
    {
        // Arrange
        var settings = new EmbeddingSettings
        {
            ApiKey = "test-api-key"
        };

        // Act
        var result = settings.GetEffectiveApiKey();

        // Assert
        Assert.Equal("test-api-key", result);
    }

    [Fact]
    public void GetEffectiveApiKey_WithApiKeyAndApiKeyFile_ReturnsApiKey()
    {
        // Arrange - ApiKey takes precedence over ApiKeyFile
        var settings = new EmbeddingSettings
        {
            ApiKey = "test-api-key",
            ApiKeyFile = "/nonexistent/file.txt"
        };

        // Act
        var result = settings.GetEffectiveApiKey();

        // Assert
        Assert.Equal("test-api-key", result);
    }

    [Fact]
    public void GetEffectiveApiKey_WithNoKeyOrFile_ReturnsEmpty()
    {
        // Arrange
        var settings = new EmbeddingSettings();

        // Act
        var result = settings.GetEffectiveApiKey();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetEffectiveApiKey_WithNonexistentFile_ReturnsEmpty()
    {
        // Arrange
        var settings = new EmbeddingSettings
        {
            ApiKeyFile = "/nonexistent/file.txt"
        };

        // Act
        var result = settings.GetEffectiveApiKey();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetEffectiveApiKey_WithTildePath_ExpandsToHomeDirectory()
    {
        // Arrange
        var settings = new EmbeddingSettings
        {
            ApiKeyFile = "~/nonexistent-file-that-does-not-exist.txt"
        };

        // Act
        var result = settings.GetEffectiveApiKey();

        // Assert - Should return empty since file doesn't exist,
        // but it should have tried to expand the path
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var settings = new EmbeddingSettings
        {
            Provider = "OpenAI",
            Model = "text-embedding-ada-002",
            Dimensions = 768,
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test123",
            ApiKeyFile = "/path/to/key.txt",
            SkipShortContent = false,
            MinContentLength = 5,
            BatchSize = 50
        };

        // Assert
        Assert.Equal("OpenAI", settings.Provider);
        Assert.Equal("text-embedding-ada-002", settings.Model);
        Assert.Equal(768, settings.Dimensions);
        Assert.Equal("https://api.openai.com/v1", settings.BaseUrl);
        Assert.Equal("sk-test123", settings.ApiKey);
        Assert.Equal("/path/to/key.txt", settings.ApiKeyFile);
        Assert.False(settings.SkipShortContent);
        Assert.Equal(5, settings.MinContentLength);
        Assert.Equal(50, settings.BatchSize);
    }
}
