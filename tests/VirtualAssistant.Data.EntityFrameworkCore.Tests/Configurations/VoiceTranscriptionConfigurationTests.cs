using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.Configurations;

public class VoiceTranscriptionConfigurationTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task VoiceTranscription_CanBeSavedAndRetrieved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var transcription = new VoiceTranscription
        {
            TranscribedText = "Test přepisu hlasu",
            SourceApp = "PushToTalk",
            DurationMs = 3500,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        context.VoiceTranscriptions.Add(transcription);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.VoiceTranscriptions.FirstAsync();
        Assert.Equal("Test přepisu hlasu", saved.TranscribedText);
        Assert.Equal("PushToTalk", saved.SourceApp);
        Assert.Equal(3500, saved.DurationMs);
    }

    [Fact]
    public async Task VoiceTranscription_MultipleCanBeSaved()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        context.VoiceTranscriptions.AddRange(
            new VoiceTranscription { TranscribedText = "First", CreatedAt = DateTime.UtcNow },
            new VoiceTranscription { TranscribedText = "Second", CreatedAt = DateTime.UtcNow },
            new VoiceTranscription { TranscribedText = "Third", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Assert
        var count = await context.VoiceTranscriptions.CountAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task VoiceTranscription_LongText_CanBeSaved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var longText = new string('a', 10000); // 10K characters
        var transcription = new VoiceTranscription
        {
            TranscribedText = longText,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        context.VoiceTranscriptions.Add(transcription);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.VoiceTranscriptions.FirstAsync();
        Assert.Equal(10000, saved.TranscribedText.Length);
    }

    [Fact]
    public async Task VoiceTranscription_AutoGeneratesId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var transcription = new VoiceTranscription
        {
            TranscribedText = "Test",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        context.VoiceTranscriptions.Add(transcription);
        await context.SaveChangesAsync();

        // Assert
        Assert.True(transcription.Id > 0);
    }

    [Fact]
    public async Task VoiceTranscription_CanQueryByCreatedAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var cutoffTime = DateTime.UtcNow;

        context.VoiceTranscriptions.AddRange(
            new VoiceTranscription { TranscribedText = "Old", CreatedAt = cutoffTime.AddDays(-1) },
            new VoiceTranscription { TranscribedText = "New", CreatedAt = cutoffTime.AddDays(1) }
        );
        await context.SaveChangesAsync();

        // Act
        var recentTranscriptions = await context.VoiceTranscriptions
            .Where(t => t.CreatedAt > cutoffTime)
            .ToListAsync();

        // Assert
        Assert.Single(recentTranscriptions);
        Assert.Equal("New", recentTranscriptions[0].TranscribedText);
    }
}
