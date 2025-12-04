using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.Queries;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.QueryHandlers;

public class ConversationByIdQueryHandlerTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task Handle_ExistingConversation_ReturnsConversationDto()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation
        {
            Title = "Test Conversation",
            CreatedAt = DateTime.UtcNow
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var handler = new ConversationByIdQueryHandler(context);
        var query = new ConversationByIdQuery { Id = conversation.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(conversation.Id, result.Id);
        Assert.Equal("Test Conversation", result.Title);
    }

    [Fact]
    public async Task Handle_NonExistentConversation_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new ConversationByIdQueryHandler(context);
        var query = new ConversationByIdQuery { Id = 999 };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithMessages_ReturnsCorrectMessageCount()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "Test", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        context.Messages.AddRange(
            new Message { ConversationId = conversation.Id, Content = "M1", Role = "user", CreatedAt = DateTime.UtcNow },
            new Message { ConversationId = conversation.Id, Content = "M2", Role = "assistant", CreatedAt = DateTime.UtcNow },
            new Message { ConversationId = conversation.Id, Content = "M3", Role = "user", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new ConversationByIdQueryHandler(context);
        var query = new ConversationByIdQuery { Id = conversation.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.MessageCount);
    }
}
