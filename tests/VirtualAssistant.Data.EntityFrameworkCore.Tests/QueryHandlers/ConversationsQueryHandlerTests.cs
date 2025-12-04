using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.Queries;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.QueryHandlers;

public class ConversationsQueryHandlerTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new ConversationsQueryHandler(context);
        var query = new ConversationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithConversations_ReturnsAllConversations()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.Conversations.AddRange(
            new Conversation { Title = "Conversation 1", CreatedAt = DateTime.UtcNow },
            new Conversation { Title = "Conversation 2", CreatedAt = DateTime.UtcNow },
            new Conversation { Title = "Conversation 3", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new ConversationsQueryHandler(context);
        var query = new ConversationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var conversations = result.ToList();
        Assert.Equal(3, conversations.Count);
    }

    [Fact]
    public async Task Handle_WithConversations_OrdersByDateDescending()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var oldest = new Conversation { Title = "Oldest", CreatedAt = DateTime.UtcNow.AddDays(-2) };
        var newest = new Conversation { Title = "Newest", CreatedAt = DateTime.UtcNow };
        var middle = new Conversation { Title = "Middle", CreatedAt = DateTime.UtcNow.AddDays(-1) };

        context.Conversations.AddRange(oldest, middle, newest);
        await context.SaveChangesAsync();

        var handler = new ConversationsQueryHandler(context);
        var query = new ConversationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var conversations = result.ToList();
        Assert.Equal("Newest", conversations[0].Title);
        Assert.Equal("Middle", conversations[1].Title);
        Assert.Equal("Oldest", conversations[2].Title);
    }

    [Fact]
    public async Task Handle_WithMessages_ReturnsCorrectMessageCount()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "With Messages", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        context.Messages.AddRange(
            new Message { ConversationId = conversation.Id, Content = "Message 1", Role = "user", CreatedAt = DateTime.UtcNow },
            new Message { ConversationId = conversation.Id, Content = "Message 2", Role = "assistant", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new ConversationsQueryHandler(context);
        var query = new ConversationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var dto = result.First();
        Assert.Equal(2, dto.MessageCount);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectDtoProperties()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var createdAt = DateTime.UtcNow.AddHours(-1);
        var updatedAt = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Title = "Test Title",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var handler = new ConversationsQueryHandler(context);
        var query = new ConversationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var dto = result.First();
        Assert.Equal(conversation.Id, dto.Id);
        Assert.Equal("Test Title", dto.Title);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(updatedAt, dto.UpdatedAt);
    }
}
