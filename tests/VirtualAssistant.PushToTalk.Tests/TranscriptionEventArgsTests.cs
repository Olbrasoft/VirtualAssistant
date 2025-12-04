using Olbrasoft.VirtualAssistant.PushToTalk;

namespace VirtualAssistant.PushToTalk.Tests;

public class TranscriptionEventArgsTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsAllProperties()
    {
        // Arrange
        var text = "Hello world";
        var confidence = 0.95f;
        var timestamp = new DateTime(2025, 1, 1, 12, 0, 0);

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(text, args.Text);
        Assert.Equal(confidence, args.Confidence);
        Assert.Equal(timestamp, args.Timestamp);
    }

    [Fact]
    public void Constructor_EmptyText_SetsEmptyString()
    {
        // Arrange
        var text = string.Empty;
        var confidence = 0.0f;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Empty(args.Text);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Constructor_DifferentConfidenceValues_PreservesValue(float confidence)
    {
        // Arrange
        var text = "Test";
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(confidence, args.Confidence);
    }

    [Fact]
    public void Constructor_CzechText_PreservesUnicode()
    {
        // Arrange
        var text = "Příliš žluťoučký kůň úpěl ďábelské ódy";
        var confidence = 0.9f;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(text, args.Text);
        Assert.Contains("ř", args.Text);
        Assert.Contains("ž", args.Text);
        Assert.Contains("ů", args.Text);
    }

    [Fact]
    public void Constructor_LowConfidence_PreservesValue()
    {
        // Arrange - values below 0 or above 1 shouldn't happen but aren't prevented
        var text = "Uncertain transcription";
        var confidence = 0.1f;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new TranscriptionEventArgs(text, confidence, timestamp);

        // Assert
        Assert.Equal(0.1f, args.Confidence);
    }
}
