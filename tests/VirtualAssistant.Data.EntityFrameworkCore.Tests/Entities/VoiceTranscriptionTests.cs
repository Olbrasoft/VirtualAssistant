using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.Entities;

public class VoiceTranscriptionTests
{
    [Fact]
    public void Constructor_SetsDefaultCreatedAt()
    {
        // Arrange & Act
        var beforeCreation = DateTime.UtcNow;
        var transcription = new VoiceTranscription { TranscribedText = "Test" };
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(transcription.CreatedAt >= beforeCreation);
        Assert.True(transcription.CreatedAt <= afterCreation);
    }

    [Fact]
    public void TranscribedText_CanBeSet()
    {
        // Arrange
        var transcription = new VoiceTranscription { TranscribedText = "Hello world" };

        // Assert
        Assert.Equal("Hello world", transcription.TranscribedText);
    }

    [Fact]
    public void SourceApp_IsNullableAndCanBeSet()
    {
        // Arrange
        var transcription1 = new VoiceTranscription { TranscribedText = "Test" };
        var transcription2 = new VoiceTranscription { TranscribedText = "Test", SourceApp = "PushToTalk" };

        // Assert
        Assert.Null(transcription1.SourceApp);
        Assert.Equal("PushToTalk", transcription2.SourceApp);
    }

    [Fact]
    public void DurationMs_IsNullableAndCanBeSet()
    {
        // Arrange
        var transcription1 = new VoiceTranscription { TranscribedText = "Test" };
        var transcription2 = new VoiceTranscription { TranscribedText = "Test", DurationMs = 2500 };

        // Assert
        Assert.Null(transcription1.DurationMs);
        Assert.Equal(2500, transcription2.DurationMs);
    }

    [Fact]
    public void Id_InheritsFromBaseEntity()
    {
        // Arrange
        var transcription = new VoiceTranscription { TranscribedText = "Test" };

        // Assert - Id should be default (0) for new entity
        Assert.Equal(0, transcription.Id);
    }
}
