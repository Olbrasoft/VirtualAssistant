using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.TranscriptionCorrectionQueryHandlers;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.QueryHandlers;

/// <summary>
/// Unit tests for TranscriptionCorrection query handlers using in-memory database.
/// </summary>
public class TranscriptionCorrectionQueryHandlerTests
{
    private static VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    #region GetActiveTranscriptionCorrectionsQueryHandler Tests

    [Fact]
    public async Task GetActiveCorrections_ReturnsOnlyActiveCorrections()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.TranscriptionCorrections.AddRange(
            new TranscriptionCorrection { IncorrectText = "active1", CorrectText = "Active1", IsActive = true, Priority = 10, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new TranscriptionCorrection { IncorrectText = "active2", CorrectText = "Active2", IsActive = true, Priority = 20, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new TranscriptionCorrection { IncorrectText = "inactive", CorrectText = "Inactive", IsActive = false, Priority = 30, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetActiveTranscriptionCorrectionsQueryHandler(context);
        var query = new GetActiveTranscriptionCorrectionsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task GetActiveCorrections_OrdersByPriorityDescending()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.TranscriptionCorrections.AddRange(
            new TranscriptionCorrection { IncorrectText = "low", CorrectText = "Low", IsActive = true, Priority = 10, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new TranscriptionCorrection { IncorrectText = "high", CorrectText = "High", IsActive = true, Priority = 100, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new TranscriptionCorrection { IncorrectText = "medium", CorrectText = "Medium", IsActive = true, Priority = 50, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetActiveTranscriptionCorrectionsQueryHandler(context);
        var query = new GetActiveTranscriptionCorrectionsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("high", result[0].IncorrectText);
        Assert.Equal("medium", result[1].IncorrectText);
        Assert.Equal("low", result[2].IncorrectText);
    }

    [Fact]
    public async Task GetActiveCorrections_ReturnsEmpty_WhenNoActiveCorrections()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.TranscriptionCorrections.Add(
            new TranscriptionCorrection { IncorrectText = "inactive", CorrectText = "Inactive", IsActive = false, Priority = 10, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetActiveTranscriptionCorrectionsQueryHandler(context);
        var query = new GetActiveTranscriptionCorrectionsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveCorrections_ThenByIdForSamePriority()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Add corrections with same priority
        context.TranscriptionCorrections.AddRange(
            new TranscriptionCorrection { IncorrectText = "first", CorrectText = "First", IsActive = true, Priority = 50, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new TranscriptionCorrection { IncorrectText = "second", CorrectText = "Second", IsActive = true, Priority = 50, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetActiveTranscriptionCorrectionsQueryHandler(context);
        var query = new GetActiveTranscriptionCorrectionsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        // Should be ordered by Id ascending after Priority
        Assert.True(result[0].Id < result[1].Id);
    }

    #endregion

    #region GetTranscriptionCorrectionByIdQueryHandler Tests

    [Fact]
    public async Task GetCorrectionById_ReturnsCorrection_WhenExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var correction = new TranscriptionCorrection
        {
            IncorrectText = "test",
            CorrectText = "Test",
            IsActive = true,
            Priority = 50,
            CaseSensitive = true,
            Notes = "Test note",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.TranscriptionCorrections.Add(correction);
        await context.SaveChangesAsync();

        var handler = new GetTranscriptionCorrectionByIdQueryHandler(context);
        var query = new GetTranscriptionCorrectionByIdQuery(correction.Id);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(correction.Id, result.Id);
        Assert.Equal("test", result.IncorrectText);
        Assert.Equal("Test", result.CorrectText);
        Assert.True(result.CaseSensitive);
        Assert.Equal("Test note", result.Notes);
    }

    [Fact]
    public async Task GetCorrectionById_ReturnsNull_WhenNotExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var handler = new GetTranscriptionCorrectionByIdQueryHandler(context);
        var query = new GetTranscriptionCorrectionByIdQuery(999);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    #endregion
}
