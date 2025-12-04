using Olbrasoft.VirtualAssistant.PushToTalk.Service.Models;

namespace VirtualAssistant.PushToTalk.Service.Tests;

public class PttEventTests
{
    [Fact]
    public void PttEvent_RecordingStarted_SetsCorrectType()
    {
        // Arrange & Act
        var evt = new PttEvent { EventType = PttEventType.RecordingStarted };

        // Assert
        Assert.Equal(PttEventType.RecordingStarted, evt.EventType);
    }

    [Fact]
    public void PttEvent_TranscriptionCompleted_IncludesTextAndConfidence()
    {
        // Arrange & Act
        var evt = new PttEvent
        {
            EventType = PttEventType.TranscriptionCompleted,
            Text = "Hello world",
            Confidence = 0.95f,
            DurationSeconds = 2.5
        };

        // Assert
        Assert.Equal(PttEventType.TranscriptionCompleted, evt.EventType);
        Assert.Equal("Hello world", evt.Text);
        Assert.Equal(0.95f, evt.Confidence);
        Assert.Equal(2.5, evt.DurationSeconds);
    }

    [Fact]
    public void PttEvent_TranscriptionFailed_IncludesErrorMessage()
    {
        // Arrange & Act
        var evt = new PttEvent
        {
            EventType = PttEventType.TranscriptionFailed,
            ErrorMessage = "Model not loaded"
        };

        // Assert
        Assert.Equal(PttEventType.TranscriptionFailed, evt.EventType);
        Assert.Equal("Model not loaded", evt.ErrorMessage);
    }

    [Fact]
    public void PttEvent_DefaultTimestamp_IsUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var evt = new PttEvent { EventType = PttEventType.RecordingStarted };
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(evt.Timestamp, before, after);
    }

    [Fact]
    public void PttEvent_DefaultServiceVersion_IsSet()
    {
        // Arrange & Act
        var evt = new PttEvent { EventType = PttEventType.RecordingStarted };

        // Assert
        Assert.Equal("1.0.0", evt.ServiceVersion);
    }

    [Theory]
    [InlineData(PttEventType.RecordingStarted)]
    [InlineData(PttEventType.RecordingStopped)]
    [InlineData(PttEventType.TranscriptionStarted)]
    [InlineData(PttEventType.TranscriptionCompleted)]
    [InlineData(PttEventType.TranscriptionFailed)]
    [InlineData(PttEventType.ManualMuteOn)]
    [InlineData(PttEventType.ManualMuteOff)]
    public void PttEventType_AllValuesAreDistinct(PttEventType eventType)
    {
        // Arrange & Act
        var evt = new PttEvent { EventType = eventType };

        // Assert
        Assert.Equal(eventType, evt.EventType);
    }

    [Fact]
    public void PttEvent_ManualMuteOn_HasCorrectType()
    {
        // Arrange & Act
        var evt = new PttEvent { EventType = PttEventType.ManualMuteOn };

        // Assert
        Assert.Equal(PttEventType.ManualMuteOn, evt.EventType);
    }

    [Fact]
    public void PttEvent_NullableProperties_AreNullByDefault()
    {
        // Arrange & Act
        var evt = new PttEvent { EventType = PttEventType.RecordingStarted };

        // Assert
        Assert.Null(evt.Text);
        Assert.Null(evt.Confidence);
        Assert.Null(evt.DurationSeconds);
        Assert.Null(evt.ErrorMessage);
    }

    [Fact]
    public void PttEvent_CzechText_PreservesUnicode()
    {
        // Arrange & Act
        var evt = new PttEvent
        {
            EventType = PttEventType.TranscriptionCompleted,
            Text = "Příliš žluťoučký kůň úpěl ďábelské ódy"
        };

        // Assert
        Assert.Contains("ř", evt.Text);
        Assert.Contains("ž", evt.Text);
        Assert.Contains("ů", evt.Text);
    }
}
