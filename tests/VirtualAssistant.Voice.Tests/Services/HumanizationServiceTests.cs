using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;
using VirtualAssistant.LlmChain;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for HumanizationService - humanizing agent notifications using LLM.
/// Tests verify filtering, LLM integration, fallback behavior, and response cleaning.
/// </summary>
public class HumanizationServiceTests
{
    private readonly Mock<ILogger<HumanizationService>> _loggerMock;
    private readonly Mock<ILlmChainClient> _llmChainMock;
    private readonly Mock<IPromptLoader> _promptLoaderMock;
    private readonly HumanizationService _sut;

    public HumanizationServiceTests()
    {
        _loggerMock = new Mock<ILogger<HumanizationService>>();
        _llmChainMock = new Mock<ILlmChainClient>();
        _promptLoaderMock = new Mock<IPromptLoader>();

        _promptLoaderMock
            .Setup(x => x.LoadPrompt("AgentNotificationHumanizer"))
            .Returns("Test system prompt");

        _sut = new HumanizationService(
            _loggerMock.Object,
            _llmChainMock.Object,
            _promptLoaderMock.Object);
    }

    [Fact]
    public async Task HumanizeAsync_WithEmptyNotifications_ReturnsNull()
    {
        // Arrange
        var notifications = new List<AgentNotification>();

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert
        Assert.Null(result);
        _llmChainMock.Verify(
            x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HumanizeAsync_WithOnlyStartNotifications_ReturnsNull()
    {
        // Arrange - start notifications are filtered out
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "start",
                Content = "Začínám pracovat..."
            }
        };

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert
        Assert.Null(result);
        _llmChainMock.Verify(
            x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HumanizeAsync_FiltersOutStartWhenCompleteExists()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "start",
                Content = "Začínám..."
            },
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Mám hotovo."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "Dokončil jsem úkol.",
                ProviderName = "Mistral",
                KeyIdentifier = "mistral-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Dokončil jsem úkol.", result);

        // Verify LLM was called with only complete notification (start filtered out)
        _llmChainMock.Verify(
            x => x.CompleteAsync(
                It.Is<LlmChainRequest>(r => r.UserMessage.Contains("complete") && !r.UserMessage.Contains("start")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HumanizeAsync_WhenLlmSucceeds_ReturnsHumanizedText()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Dokončil jsem úkol #42."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "Mám hotovo s úkolem 42.",
                ProviderName = "Mistral",
                KeyIdentifier = "mistral-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo s úkolem 42.", result);

        // Verify LLM request parameters
        _llmChainMock.Verify(
            x => x.CompleteAsync(
                It.Is<LlmChainRequest>(r =>
                    r.SystemPrompt == "Test system prompt" &&
                    r.Temperature == 0.3f &&
                    r.MaxTokens == 150),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HumanizeAsync_WhenLlmFails_UsesFallback()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Task completed."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = false,
                Error = "Rate limit exceeded"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - fallback returns "Mám hotovo." for single complete notification
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo.", result);
    }

    [Fact]
    public async Task HumanizeAsync_WhenLlmReturnsEmptyResponse_UsesFallback()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Done"
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "   ",  // Whitespace only
                ProviderName = "Mistral",
                KeyIdentifier = "mistral-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo.", result);
    }

    [Fact]
    public async Task HumanizeAsync_WhenLlmReturnsEmptyIndicator_ReturnsNull()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "status",
                Content = "Working..."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "(prázdný)",
                ProviderName = "Mistral",
                KeyIdentifier = "mistral-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - LLM indicated to skip this notification
        Assert.Null(result);
    }

    [Fact]
    public async Task HumanizeAsync_CleansThinkingTags()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Task done."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "<think>Let me formulate this...</think>Mám hotovo s úkolem.",
                ProviderName = "Cerebras",
                KeyIdentifier = "cerebras-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - <think> tags are removed
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo s úkolem.", result);
        Assert.DoesNotContain("<think>", result);
        Assert.DoesNotContain("</think>", result);
    }

    [Fact]
    public async Task HumanizeAsync_CleansUnclosedThinkingTags()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Task done."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "Mám hotovo s úkolem.<think>Unclosed thinking tag...",
                ProviderName = "Cerebras",
                KeyIdentifier = "cerebras-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - unclosed <think> tag and everything after it is removed
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo s úkolem.", result);
        Assert.DoesNotContain("<think>", result);
    }

    [Fact]
    public async Task HumanizeAsync_WithIssueSummaries_IncludesThemInRequest()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Dokončil jsem úkol #42."
            }
        };

        var issueSummaries = new Dictionary<int, IssueSummaryInfo>
        {
            [42] = new IssueSummaryInfo
            {
                IssueNumber = 42,
                CzechTitle = "Přidat unit testy",
                CzechSummary = "Testy pro HumanizationService",
                IsOpen = true
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "Přidal jsem unit testy pro HumanizationService.",
                ProviderName = "Mistral",
                KeyIdentifier = "mistral-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications, issueSummaries);

        // Assert
        Assert.NotNull(result);

        // Verify LLM request includes issue summaries in JSON format
        _llmChainMock.Verify(
            x => x.CompleteAsync(
                It.Is<LlmChainRequest>(r => r.UserMessage.Contains("relatedIssues")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HumanizeAsync_WithMultipleStatusNotifications_UsesFallbackCorrectly()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "status",
                Content = "Začínám pracovat na úkolu."
            },
            new()
            {
                Agent = "claude-code",
                Type = "status",
                Content = "Pokračuji v práci."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - fallback concatenates status notification content
        Assert.NotNull(result);
        Assert.Contains("Začínám pracovat na úkolu.", result);
        Assert.Contains("Pokračuji v práci.", result);
    }

    [Fact]
    public async Task HumanizeAsync_WithMultipleCompleteNotifications_UsesFallbackCorrectly()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Task 1 done."
            },
            new()
            {
                Agent = "opencode",
                Type = "complete",
                Content = "Task 2 done."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - fallback returns "Mám hotovo s oběma úkoly." for 2 complete notifications
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo s oběma úkoly.", result);
    }

    [Fact]
    public async Task HumanizeAsync_WithThreeCompleteNotifications_UsesFallbackCorrectly()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Task 1 done."
            },
            new()
            {
                Agent = "opencode",
                Type = "complete",
                Content = "Task 2 done."
            },
            new()
            {
                Agent = "github-agent",
                Type = "complete",
                Content = "Task 3 done."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - fallback returns "Mám hotovo se všemi úkoly." for 3+ complete notifications
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo se všemi úkoly.", result);
    }

    [Fact]
    public async Task HumanizeAsync_WhenExceptionThrown_UsesFallback()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Task done."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - exception is caught and fallback is used
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo.", result);
    }

    [Fact]
    public async Task HumanizeAsync_RemovesXmlTags()
    {
        // Arrange
        var notifications = new List<AgentNotification>
        {
            new()
            {
                Agent = "claude-code",
                Type = "complete",
                Content = "Task done."
            }
        };

        _llmChainMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChainResult
            {
                Success = true,
                Content = "Mám <tag>hotovo</tag> s úkolem.",
                ProviderName = "Mistral",
                KeyIdentifier = "mistral-free"
            });

        // Act
        var result = await _sut.HumanizeAsync(notifications);

        // Assert - XML tags are removed
        Assert.NotNull(result);
        Assert.Equal("Mám hotovo s úkolem.", result);
        Assert.DoesNotContain("<tag>", result);
        Assert.DoesNotContain("</tag>", result);
    }
}
