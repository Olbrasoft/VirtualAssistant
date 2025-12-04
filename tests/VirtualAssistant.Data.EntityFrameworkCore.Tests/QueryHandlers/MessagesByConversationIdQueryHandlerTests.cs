using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.Queries;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.QueryHandlers;

public class MessagesByConversationIdQueryHandlerTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task Handle_ConversationWithMessages_ReturnsMessages()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "Test", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        context.Messages.AddRange(
            new Message { ConversationId = conversation.Id, Content = "Hello", Role = "user", CreatedAt = DateTime.UtcNow },
            new Message { ConversationId = conversation.Id, Content = "Hi there!", Role = "assistant", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new MessagesByConversationIdQueryHandler(context);
        var query = new MessagesByConversationIdQuery { ConversationId = conversation.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var messages = result.ToList();
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task Handle_ConversationWithNoMessages_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "Empty", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var handler = new MessagesByConversationIdQueryHandler(context);
        var query = new MessagesByConversationIdQuery { ConversationId = conversation.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_Messages_OrderedByCreatedAtAscending()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "Test", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var oldest = new Message
        {
            ConversationId = conversation.Id,
            Content = "First",
            Role = "user",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var newest = new Message
        {
            ConversationId = conversation.Id,
            Content = "Third",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        var middle = new Message
        {
            ConversationId = conversation.Id,
            Content = "Second",
            Role = "assistant",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        context.Messages.AddRange(newest, oldest, middle);
        await context.SaveChangesAsync();

        var handler = new MessagesByConversationIdQueryHandler(context);
        var query = new MessagesByConversationIdQuery { ConversationId = conversation.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var messages = result.ToList();
        Assert.Equal("First", messages[0].Content);
        Assert.Equal("Second", messages[1].Content);
        Assert.Equal("Third", messages[2].Content);
    }

    [Fact]
    public async Task Handle_OnlyReturnsMessagesForSpecifiedConversation()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation1 = new Conversation { Title = "Conv 1", CreatedAt = DateTime.UtcNow };
        var conversation2 = new Conversation { Title = "Conv 2", CreatedAt = DateTime.UtcNow };
        context.Conversations.AddRange(conversation1, conversation2);
        await context.SaveChangesAsync();

        context.Messages.AddRange(
            new Message { ConversationId = conversation1.Id, Content = "Conv1 Msg", Role = "user", CreatedAt = DateTime.UtcNow },
            new Message { ConversationId = conversation2.Id, Content = "Conv2 Msg", Role = "user", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new MessagesByConversationIdQueryHandler(context);
        var query = new MessagesByConversationIdQuery { ConversationId = conversation1.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var messages = result.ToList();
        Assert.Single(messages);
        Assert.Equal("Conv1 Msg", messages[0].Content);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectDtoProperties()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "Test", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var createdAt = DateTime.UtcNow;
        var message = new Message
        {
            ConversationId = conversation.Id,
            Content = "Test content",
            Role = "assistant",
            CreatedAt = createdAt
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();

        var handler = new MessagesByConversationIdQueryHandler(context);
        var query = new MessagesByConversationIdQuery { ConversationId = conversation.Id };

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var dto = result.First();
        Assert.Equal(message.Id, dto.Id);
        Assert.Equal(conversation.Id, dto.ConversationId);
        Assert.Equal("Test content", dto.Content);
        Assert.Equal("assistant", dto.Role);
        Assert.Equal(createdAt, dto.CreatedAt);
    }
}
