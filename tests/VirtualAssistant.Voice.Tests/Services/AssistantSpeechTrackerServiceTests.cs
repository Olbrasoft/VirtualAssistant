using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Text.Similarity;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

public class AssistantSpeechTrackerServiceTests
{
    private readonly Mock<ILogger<AssistantSpeechTrackerService>> _loggerMock;
    private readonly Mock<IStringSimilarity> _similarityMock;
    private readonly AssistantSpeechTrackerService _sut;

    public AssistantSpeechTrackerServiceTests()
    {
        _loggerMock = new Mock<ILogger<AssistantSpeechTrackerService>>();
        _similarityMock = new Mock<IStringSimilarity>();
        
        // Default similarity: return 1.0 for identical strings, 0.0 otherwise
        _similarityMock
            .Setup(x => x.Similarity(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string a, string b) => a == b ? 1.0 : 0.0);
            
        _sut = new AssistantSpeechTrackerService(_loggerMock.Object, _similarityMock.Object);
    }

    #region IsSpeaking Tests

    [Fact]
    public void IsSpeaking_InitialState_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(_sut.IsSpeaking);
    }

    [Fact]
    public void IsSpeaking_AfterStartSpeaking_ReturnsTrue()
    {
        // Arrange
        _sut.StartSpeaking("Test message");
        
        // Act & Assert
        Assert.True(_sut.IsSpeaking);
    }

    [Fact]
    public void IsSpeaking_AfterStopSpeaking_ReturnsFalse()
    {
        // Arrange
        _sut.StartSpeaking("Test message");
        _sut.StopSpeaking();
        
        // Act & Assert
        Assert.False(_sut.IsSpeaking);
    }

    [Fact]
    public void StartSpeaking_EmptyText_DoesNotSetSpeaking()
    {
        // Arrange & Act
        _sut.StartSpeaking("");
        _sut.StartSpeaking("   ");
        _sut.StartSpeaking(null!);
        
        // Assert
        Assert.Equal(0, _sut.GetHistoryCount());
    }

    #endregion

    #region History Management Tests

    [Fact]
    public void GetHistoryCount_InitialState_ReturnsZero()
    {
        // Act & Assert
        Assert.Equal(0, _sut.GetHistoryCount());
    }

    [Fact]
    public void StartSpeaking_AddsToHistory()
    {
        // Arrange & Act
        _sut.StartSpeaking("First message");
        _sut.StartSpeaking("Second message");
        
        // Assert
        Assert.Equal(2, _sut.GetHistoryCount());
    }

    [Fact]
    public void ClearHistory_RemovesAllMessages()
    {
        // Arrange
        _sut.StartSpeaking("First message");
        _sut.StartSpeaking("Second message");
        Assert.Equal(2, _sut.GetHistoryCount());
        
        // Act
        _sut.ClearHistory();
        
        // Assert
        Assert.Equal(0, _sut.GetHistoryCount());
    }

    [Fact]
    public void StartSpeaking_LimitsHistoryToMaxSize()
    {
        // Arrange - add 15 messages (max is 10)
        for (int i = 0; i < 15; i++)
        {
            _sut.StartSpeaking($"Message {i}");
        }
        
        // Assert - should be limited to 10
        Assert.Equal(10, _sut.GetHistoryCount());
    }

    #endregion

    #region FilterEchoFromTranscription Tests

    [Fact]
    public void FilterEchoFromTranscription_EmptyTranscription_ReturnsEmpty()
    {
        // Arrange
        _sut.StartSpeaking("Some TTS message");
        
        // Act
        var result = _sut.FilterEchoFromTranscription("");
        
        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void FilterEchoFromTranscription_NoHistory_ReturnsOriginal()
    {
        // Arrange
        var transcription = "Hello, this is user speech";
        
        // Act
        var result = _sut.FilterEchoFromTranscription(transcription);
        
        // Assert
        Assert.Equal(transcription, result);
    }

    [Fact]
    public void FilterEchoFromTranscription_ExactMatch_ReturnsEmpty()
    {
        // Arrange
        var ttsMessage = "This is what the assistant said";
        _sut.StartSpeaking(ttsMessage);
        
        // Setup similarity to return high value for matching text
        _similarityMock
            .Setup(x => x.Similarity(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0.95);
        
        // Act
        var result = _sut.FilterEchoFromTranscription(ttsMessage);
        
        // Assert
        Assert.Equal("", result.Trim());
    }

    [Fact]
    public void FilterEchoFromTranscription_NoMatch_ReturnsOriginal()
    {
        // Arrange
        _sut.StartSpeaking("TTS output message");
        
        // Setup similarity to return low value (no match)
        _similarityMock
            .Setup(x => x.Similarity(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0.1);
        
        var userSpeech = "Completely different user input";
        
        // Act
        var result = _sut.FilterEchoFromTranscription(userSpeech);
        
        // Assert
        Assert.Equal(userSpeech.Trim(), result);
    }

    #endregion

    #region ContainsStopWord Tests

    [Fact]
    public void ContainsStopWord_EmptyHistory_ReturnsFalse()
    {
        // Arrange
        var stopWords = new[] { "stop", "halt" };
        
        // Act
        var result = _sut.ContainsStopWord(stopWords);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsStopWord_HistoryContainsStopWord_ReturnsTrue()
    {
        // Arrange
        _sut.StartSpeaking("Please stop the process");
        var stopWords = new[] { "stop", "halt" };
        
        // Act
        var result = _sut.ContainsStopWord(stopWords);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsStopWord_HistoryDoesNotContainStopWord_ReturnsFalse()
    {
        // Arrange
        _sut.StartSpeaking("Continue with the process");
        var stopWords = new[] { "stop", "halt" };
        
        // Act
        var result = _sut.ContainsStopWord(stopWords);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsStopWord_CaseInsensitive()
    {
        // Arrange
        _sut.StartSpeaking("STOP immediately!");
        var stopWords = new[] { "stop" };
        
        // Act
        var result = _sut.ContainsStopWord(stopWords);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsStopWord_ChecksAllHistoryMessages()
    {
        // Arrange
        _sut.StartSpeaking("First message without keyword");
        _sut.StartSpeaking("Second message with halt command");
        var stopWords = new[] { "stop", "halt" };
        
        // Act
        var result = _sut.ContainsStopWord(stopWords);
        
        // Assert
        Assert.True(result);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Operations_AreThreadSafe()
    {
        // Arrange
        var iterations = 100;
        var tasks = new List<Task>();
        
        // Act - multiple threads performing operations simultaneously
        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    _sut.StartSpeaking($"Message from thread {threadId}");
                    _sut.GetHistoryCount();
                    _sut.FilterEchoFromTranscription("Test transcription");
                    _ = _sut.IsSpeaking;
                }
            }));
        }
        
        // Assert - no exception means thread safety works
        await Task.WhenAll(tasks);
    }

    #endregion

    #region Legacy Methods Tests

    [Fact]
    public void GetCurrentSpeechText_EmptyHistory_ReturnsNull()
    {
        // Act
#pragma warning disable CS0618 // Type or member is obsolete
        var result = _sut.GetCurrentSpeechText();
#pragma warning restore CS0618
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCurrentSpeechText_WithHistory_ReturnsLastMessage()
    {
        // Arrange
        _sut.StartSpeaking("First");
        _sut.StartSpeaking("Second");
        _sut.StartSpeaking("Third");
        
        // Act
#pragma warning disable CS0618 // Type or member is obsolete
        var result = _sut.GetCurrentSpeechText();
#pragma warning restore CS0618
        
        // Assert
        Assert.Equal("Third", result);
    }

    #endregion
}
