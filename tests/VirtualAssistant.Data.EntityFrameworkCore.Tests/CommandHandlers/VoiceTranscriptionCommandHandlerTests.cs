using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Commands.VoiceTranscriptionCommands;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.VoiceTranscriptionCommandHandlers;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.CommandHandlers;

/// <summary>
/// Unit tests for VoiceTranscription command handlers using SQLite in-memory database.
/// SQLite validates FK constraints (InMemory does not).
/// </summary>
public class VoiceTranscriptionCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VirtualAssistantDbContext _dbContext;

    public VoiceTranscriptionCommandHandlerTests()
    {
        // SQLite in-memory requires open connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new VirtualAssistantDbContext(options);
        _dbContext.Database.EnsureCreated(); // Create schema with FK constraints + seeded data
        // Note: Providers are seeded via HasData() in ProviderConfiguration
    }

    #region SaveVoiceTranscriptionCommandHandler Tests

    [Fact]
    public async Task SaveHandler_WithValidWhisperProvider_SavesTranscription()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var command = new SaveVoiceTranscriptionCommand(
            Text: "Test transcription",
            DurationMs: 1500,
            ProviderId: 13  // Whisper Local - exists
        );

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("Test transcription", result.TranscribedText);
        Assert.Equal(13, result.ProviderId);
        Assert.Equal(1500, result.AudioDurationMs);
    }

    [Fact]
    public async Task SaveHandler_WithValidGoogleProvider_SavesTranscription()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var command = new SaveVoiceTranscriptionCommand(
            Text: "Ahoj světe",
            DurationMs: 2000,
            ProviderId: 14  // Google Speech-to-Text - exists
        );

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("Ahoj světe", result.TranscribedText);
        Assert.Equal(14, result.ProviderId);
        Assert.Equal(2000, result.AudioDurationMs);
    }

    [Fact]
    public async Task SaveHandler_WithInvalidProvider_ThrowsDbUpdateException()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var command = new SaveVoiceTranscriptionCommand(
            Text: "Test",
            DurationMs: 1000,
            ProviderId: 999  // Does NOT exist!
        );

        // Act & Assert - SQLite validates FK constraint!
        await Assert.ThrowsAsync<DbUpdateException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task SaveHandler_WithNullDuration_SavesWithNullDuration()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var command = new SaveVoiceTranscriptionCommand(
            Text: "Text without duration",
            DurationMs: null,
            ProviderId: 13
        );

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Null(result.AudioDurationMs);
    }

    [Fact]
    public async Task SaveHandler_SetsCreatedAtToUtcNow()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var command = new SaveVoiceTranscriptionCommand(
            Text: "Time test",
            DurationMs: 1000,
            ProviderId: 13
        );
        var beforeSave = DateTime.UtcNow;

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);
        var afterSave = DateTime.UtcNow;

        // Assert
        Assert.True(result.CreatedAt >= beforeSave);
        Assert.True(result.CreatedAt <= afterSave);
    }

    [Fact]
    public async Task SaveHandler_PersistsToDatabase()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var command = new SaveVoiceTranscriptionCommand(
            Text: "Persistence test",
            DurationMs: 3000,
            ProviderId: 14
        );

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert - Verify persisted in database
        var persisted = await _dbContext.VoiceTranscriptions.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Persistence test", persisted.TranscribedText);
        Assert.Equal(14, persisted.ProviderId);
        Assert.Equal(3000, persisted.AudioDurationMs);
    }

    [Fact]
    public async Task SaveHandler_WithUnicodeText_PreservesUnicode()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var unicodeText = "Příliš žluťoučký kůň úpěl ďábelské ódy 日本語 emoji: 🎤";
        var command = new SaveVoiceTranscriptionCommand(
            Text: unicodeText,
            DurationMs: 5000,
            ProviderId: 13
        );

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(unicodeText, result.TranscribedText);
    }

    [Fact]
    public async Task SaveHandler_MultipleSaves_AssignsUniqueIds()
    {
        // Arrange
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);

        // Act
        var result1 = await handler.HandleAsync(
            new SaveVoiceTranscriptionCommand("First", 100, 13), CancellationToken.None);
        var result2 = await handler.HandleAsync(
            new SaveVoiceTranscriptionCommand("Second", 200, 14), CancellationToken.None);
        var result3 = await handler.HandleAsync(
            new SaveVoiceTranscriptionCommand("Third", 300, 13), CancellationToken.None);

        // Assert
        Assert.NotEqual(result1.Id, result2.Id);
        Assert.NotEqual(result2.Id, result3.Id);
        Assert.NotEqual(result1.Id, result3.Id);
    }

    [Fact]
    public async Task SaveHandler_WithMaxLengthText_Saves()
    {
        // Arrange - Text at max allowed length (2000 chars per VoiceTranscriptionConfiguration)
        var handler = new SaveVoiceTranscriptionCommandHandler(_dbContext);
        var maxText = new string('A', 2000);
        var command = new SaveVoiceTranscriptionCommand(
            Text: maxText,
            DurationMs: 300000, // 5 minutes
            ProviderId: 14
        );

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal(maxText, result.TranscribedText);
    }

    #endregion

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }
}
