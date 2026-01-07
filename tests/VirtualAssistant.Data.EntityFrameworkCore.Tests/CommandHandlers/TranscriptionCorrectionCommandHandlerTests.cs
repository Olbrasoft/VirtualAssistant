using Microsoft.EntityFrameworkCore;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.TranscriptionCorrectionCommandHandlers;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.CommandHandlers;

/// <summary>
/// Unit tests for TranscriptionCorrection command handlers using in-memory database.
/// </summary>
public class TranscriptionCorrectionCommandHandlerTests
{
    private static VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    #region AddTranscriptionCorrectionCommandHandler Tests

    [Fact]
    public async Task AddHandler_WithValidCorrection_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new AddTranscriptionCorrectionCommandHandler(context);
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "vyspru",
            CorrectText = "Whisper",
            CaseSensitive = false,
            Priority = 100,
            IsActive = true
        };
        // Use mock executor to avoid null validation in BaseCommand
        var command = new AddTranscriptionCorrectionCommand(new Mock<ICommandExecutor>().Object) { Correction = correction };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(correction.Id > 0);
        Assert.True(correction.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(correction.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task AddHandler_WithValidCorrection_SavesCorrection()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new AddTranscriptionCorrectionCommandHandler(context);
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "kapslok",
            CorrectText = "Caps Lock",
            CaseSensitive = true,
            Priority = 50,
            IsActive = true,
            Notes = "Test note"
        };
        var command = new AddTranscriptionCorrectionCommand(new Mock<ICommandExecutor>().Object) { Correction = correction };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var saved = await context.TranscriptionCorrections.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("kapslok", saved.IncorrectText);
        Assert.Equal("Caps Lock", saved.CorrectText);
        Assert.True(saved.CaseSensitive);
        Assert.Equal(50, saved.Priority);
        Assert.Equal("Test note", saved.Notes);
    }

    [Fact]
    public async Task AddHandler_SetsCreatedAtAndUpdatedAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new AddTranscriptionCorrectionCommandHandler(context);
        var beforeTime = DateTimeOffset.UtcNow;
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "test",
            CorrectText = "Test"
        };
        var command = new AddTranscriptionCorrectionCommand(new Mock<ICommandExecutor>().Object) { Correction = correction };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);
        var afterTime = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(correction.CreatedAt >= beforeTime);
        Assert.True(correction.CreatedAt <= afterTime);
        // UpdatedAt should be >= CreatedAt and within the test time window
        Assert.True(correction.UpdatedAt >= correction.CreatedAt);
        Assert.True(correction.UpdatedAt <= afterTime);
    }

    #endregion

    #region UpdateTranscriptionCorrectionCommandHandler Tests

    [Fact]
    public async Task UpdateHandler_WithValidCorrection_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "original",
            CorrectText = "Original",
            Priority = 10,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.TranscriptionCorrections.Add(correction);
        await context.SaveChangesAsync();

        var handler = new UpdateTranscriptionCorrectionCommandHandler(context);
        correction.CorrectText = "Updated";
        correction.Priority = 20;
        var command = new UpdateTranscriptionCorrectionCommand(correction);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UpdateHandler_UpdatesAllFields()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "old",
            CorrectText = "Old",
            CaseSensitive = false,
            Priority = 5,
            IsActive = true,
            Notes = "Old note",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        context.TranscriptionCorrections.Add(correction);
        await context.SaveChangesAsync();

        var handler = new UpdateTranscriptionCorrectionCommandHandler(context);
        correction.CorrectText = "New";
        correction.CaseSensitive = true;
        correction.Priority = 100;
        correction.IsActive = false;
        correction.Notes = "New note";
        var command = new UpdateTranscriptionCorrectionCommand(correction);

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var updated = await context.TranscriptionCorrections.FindAsync(correction.Id);
        Assert.NotNull(updated);
        Assert.Equal("New", updated.CorrectText);
        Assert.True(updated.CaseSensitive);
        Assert.Equal(100, updated.Priority);
        Assert.False(updated.IsActive);
        Assert.Equal("New note", updated.Notes);
    }

    [Fact]
    public async Task UpdateHandler_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var originalUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "test",
            CorrectText = "Test",
            CreatedAt = originalUpdatedAt,
            UpdatedAt = originalUpdatedAt
        };
        context.TranscriptionCorrections.Add(correction);
        await context.SaveChangesAsync();

        var handler = new UpdateTranscriptionCorrectionCommandHandler(context);
        var command = new UpdateTranscriptionCorrectionCommand(correction);

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(correction.UpdatedAt > originalUpdatedAt);
    }

    #endregion

    #region DeleteTranscriptionCorrectionCommandHandler Tests

    [Fact]
    public async Task DeleteHandler_WithExistingCorrection_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "to delete",
            CorrectText = "To Delete",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.TranscriptionCorrections.Add(correction);
        await context.SaveChangesAsync();

        var handler = new DeleteTranscriptionCorrectionCommandHandler(context);
        var command = new DeleteTranscriptionCorrectionCommand(correction.Id);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteHandler_WithExistingCorrection_RemovesFromDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var correction = new TranscriptionCorrection
        {
            IncorrectText = "to delete",
            CorrectText = "To Delete",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.TranscriptionCorrections.Add(correction);
        await context.SaveChangesAsync();
        var correctionId = correction.Id;

        var handler = new DeleteTranscriptionCorrectionCommandHandler(context);
        var command = new DeleteTranscriptionCorrectionCommand(correctionId);

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var deleted = await context.TranscriptionCorrections.FindAsync(correctionId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteHandler_WithNonExistingCorrection_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new DeleteTranscriptionCorrectionCommandHandler(context);
        var command = new DeleteTranscriptionCorrectionCommand(999);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteHandler_WithZeroId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new DeleteTranscriptionCorrectionCommandHandler(context);
        var command = new DeleteTranscriptionCorrectionCommand(0);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteHandler_WithNegativeId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new DeleteTranscriptionCorrectionCommandHandler(context);
        var command = new DeleteTranscriptionCorrectionCommand(-1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    #endregion
}
