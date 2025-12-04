using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

public class PromptLoaderTests : IDisposable
{
    private readonly Mock<ILogger<PromptLoader>> _loggerMock;
    private readonly string _testPromptsDir;
    private readonly PromptLoader _sut;

    public PromptLoaderTests()
    {
        _loggerMock = new Mock<ILogger<PromptLoader>>();
        
        // Create temp directory for test prompts
        _testPromptsDir = Path.Combine(Path.GetTempPath(), $"PromptLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPromptsDir);
        
        // Create a testable PromptLoader by setting up the environment
        // We'll use reflection or create test files in expected location
        _sut = new PromptLoader(_loggerMock.Object);
    }

    public void Dispose()
    {
        // Cleanup temp directory
        if (Directory.Exists(_testPromptsDir))
        {
            Directory.Delete(_testPromptsDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPrompt_ExistingPrompt_ReturnsContent()
    {
        // Arrange - use actual prompts from the project
        // The PromptLoader should find VoiceRouterSystem.md
        
        // Act
        var result = _sut.LoadPrompt("VoiceRouterSystem");
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("Voice Router", result);
    }

    [Fact]
    public void LoadPrompt_NonExistentPrompt_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPrompt = "NonExistentPrompt_" + Guid.NewGuid();
        
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _sut.LoadPrompt(nonExistentPrompt));
    }

    [Fact]
    public void LoadPrompt_CalledTwice_ReturnsCachedContent()
    {
        // Arrange
        var promptName = "VoiceRouterSystem";
        
        // Act
        var result1 = _sut.LoadPrompt(promptName);
        var result2 = _sut.LoadPrompt(promptName);
        
        // Assert
        Assert.Same(result1, result2); // Should be same reference due to caching
    }

    [Fact]
    public void LoadPromptWithValues_ReplacesPlaceholders()
    {
        // Arrange
        var promptName = "VoiceRouterSystem";
        var values = new Dictionary<string, string>
        {
            { "CurrentTime", "14:30" },
            { "CurrentDate", "2025-12-04" },
            { "DayOfWeek", "čtvrtek" }
        };
        
        // Act
        var result = _sut.LoadPromptWithValues(promptName, values);
        
        // Assert
        Assert.Contains("14:30", result);
        Assert.Contains("2025-12-04", result);
        Assert.Contains("čtvrtek", result);
        Assert.DoesNotContain("{{CurrentTime}}", result);
        Assert.DoesNotContain("{{CurrentDate}}", result);
        Assert.DoesNotContain("{{DayOfWeek}}", result);
    }

    [Fact]
    public void LoadPromptWithValues_EmptyValues_ReturnsUnmodifiedTemplate()
    {
        // Arrange
        var promptName = "VoiceRouterSystem";
        var values = new Dictionary<string, string>();
        
        // Act
        var result = _sut.LoadPromptWithValues(promptName, values);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("{{CurrentTime}}", result); // Placeholder should remain
    }

    [Fact]
    public void LoadPromptWithValues_PartialValues_ReplacesOnlyProvidedPlaceholders()
    {
        // Arrange
        var promptName = "VoiceRouterSystem";
        var values = new Dictionary<string, string>
        {
            { "CurrentTime", "10:00" }
        };
        
        // Act
        var result = _sut.LoadPromptWithValues(promptName, values);
        
        // Assert
        Assert.Contains("10:00", result);
        Assert.Contains("{{CurrentDate}}", result); // Not replaced
    }

    [Fact]
    public void LoadPrompt_DiscussionActiveWarning_ReturnsContent()
    {
        // Arrange & Act
        var result = _sut.LoadPrompt("DiscussionActiveWarning");
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
