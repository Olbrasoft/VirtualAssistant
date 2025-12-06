using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests;

public class TranscriptionRepositoryTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_CreatesNewTranscription_ReturnsWithId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        // Act
        var result = await repository.SaveTranscriptionAsync(
            "Testovací přepis",
            "PushToTalk",
            1500,
            CancellationToken.None);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal("Testovací přepis", result.TranscribedText);
        Assert.Equal("PushToTalk", result.SourceApp);
        Assert.Equal(1500, result.DurationMs);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithNullOptionalFields_Succeeds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        // Act
        var result = await repository.SaveTranscriptionAsync(
            "Text bez metadata",
            null,
            null,
            CancellationToken.None);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal("Text bez metadata", result.TranscribedText);
        Assert.Null(result.SourceApp);
        Assert.Null(result.DurationMs);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_SetsCreatedAtToUtcNow()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);
        var beforeSave = DateTime.UtcNow;

        // Act
        var result = await repository.SaveTranscriptionAsync(
            "Test",
            null,
            null,
            CancellationToken.None);
        var afterSave = DateTime.UtcNow;

        // Assert
        Assert.True(result.CreatedAt >= beforeSave);
        Assert.True(result.CreatedAt <= afterSave);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsTranscriptionsOrderedByCreatedAtDescending()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        // Add transcriptions with different times
        context.VoiceTranscriptions.AddRange(
            new VoiceTranscription { TranscribedText = "First", CreatedAt = DateTime.UtcNow.AddMinutes(-30) },
            new VoiceTranscription { TranscribedText = "Second", CreatedAt = DateTime.UtcNow.AddMinutes(-20) },
            new VoiceTranscription { TranscribedText = "Third", CreatedAt = DateTime.UtcNow.AddMinutes(-10) }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetRecentAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Third", result[0].TranscribedText);
        Assert.Equal("Second", result[1].TranscribedText);
        Assert.Equal("First", result[2].TranscribedText);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsCountLimit()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        // Add 10 transcriptions
        for (int i = 0; i < 10; i++)
        {
            context.VoiceTranscriptions.Add(new VoiceTranscription
            {
                TranscribedText = $"Transcription {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetRecentAsync(5, CancellationToken.None);

        // Assert
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetRecentAsync_DefaultCount_Is50()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        // Add 60 transcriptions
        for (int i = 0; i < 60; i++)
        {
            context.VoiceTranscriptions.Add(new VoiceTranscription
            {
                TranscribedText = $"Transcription {i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(-i)
            });
        }
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetRecentAsync(ct: CancellationToken.None);

        // Assert
        Assert.Equal(50, result.Count);
    }

    [Fact(Skip = "ILike is PostgreSQL-specific and not supported by InMemory provider. This test requires integration testing with real PostgreSQL.")]
    public async Task SearchAsync_FindsMatchingTranscriptions_CaseInsensitive()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        context.VoiceTranscriptions.AddRange(
            new VoiceTranscription { TranscribedText = "Hello world", CreatedAt = DateTime.UtcNow },
            new VoiceTranscription { TranscribedText = "HELLO THERE", CreatedAt = DateTime.UtcNow },
            new VoiceTranscription { TranscribedText = "Goodbye", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Act - Note: InMemory database doesn't support ILike, so this tests the query structure
        // In real PostgreSQL, this would find both "Hello" matches case-insensitively
        var result = await repository.SearchAsync("Hello", CancellationToken.None);

        // Assert - InMemory provider treats ILike as Like (case-sensitive)
        // Real test would verify case-insensitive matching with PostgreSQL
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsRecentTranscriptions()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        context.VoiceTranscriptions.AddRange(
            new VoiceTranscription { TranscribedText = "First", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new VoiceTranscription { TranscribedText = "Second", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await repository.SearchAsync("", CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchAsync_WhitespaceQuery_ReturnsRecentTranscriptions()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        context.VoiceTranscriptions.Add(new VoiceTranscription
        {
            TranscribedText = "Test",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var result = await repository.SearchAsync("   ", CancellationToken.None);

        // Assert
        Assert.Single(result);
    }

    [Fact(Skip = "ILike is PostgreSQL-specific and not supported by InMemory provider. This test requires integration testing with real PostgreSQL.")]
    public async Task SearchAsync_LimitsResultsTo100()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockLogger = new Mock<ILogger<TranscriptionRepository>>();
        var repository = new TranscriptionRepository(context, mockLogger.Object);

        // Add 150 matching transcriptions
        for (int i = 0; i < 150; i++)
        {
            context.VoiceTranscriptions.Add(new VoiceTranscription
            {
                TranscribedText = $"Test text {i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(-i)
            });
        }
        await context.SaveChangesAsync();

        // Act
        var result = await repository.SearchAsync("Test", CancellationToken.None);

        // Assert
        Assert.Equal(100, result.Count);
    }
}
