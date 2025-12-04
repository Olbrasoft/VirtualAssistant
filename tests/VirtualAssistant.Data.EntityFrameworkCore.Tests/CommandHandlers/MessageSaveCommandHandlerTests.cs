using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.Commands;
using VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.CommandHandlers;

public class MessageSaveCommandHandlerTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task Handle_NewMessage_CreatesAndReturnsId()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // First create a conversation
        var conversation = new Conversation { Title = "Test", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var handler = new MessageSaveCommandHandler(context);
        var command = new MessageSaveCommand
        {
            ConversationId = conversation.Id,
            Content = "Hello, World!",
            Role = "user"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result > 0);
        var savedMessage = await context.Messages.FirstOrDefaultAsync();
        Assert.NotNull(savedMessage);
        Assert.Equal("Hello, World!", savedMessage.Content);
        Assert.Equal("user", savedMessage.Role);
        Assert.Equal(conversation.Id, savedMessage.ConversationId);
    }

    [Fact]
    public async Task Handle_NewMessage_UpdatesConversationUpdatedAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "Test", CreatedAt = DateTime.UtcNow.AddHours(-1) };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var handler = new MessageSaveCommandHandler(context);
        var beforeSave = DateTime.UtcNow;
        var command = new MessageSaveCommand
        {
            ConversationId = conversation.Id,
            Content = "New message",
            Role = "user"
        };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var updatedConversation = await context.Conversations.FirstOrDefaultAsync(c => c.Id == conversation.Id);
        Assert.NotNull(updatedConversation);
        Assert.NotNull(updatedConversation.UpdatedAt);
        Assert.True(updatedConversation.UpdatedAt >= beforeSave);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    [InlineData("system")]
    public async Task Handle_DifferentRoles_SavesCorrectly(string role)
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var conversation = new Conversation { Title = "Test", CreatedAt = DateTime.UtcNow };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var handler = new MessageSaveCommandHandler(context);
        var command = new MessageSaveCommand
        {
            ConversationId = conversation.Id,
            Content = "Test message",
            Role = role
        };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var savedMessage = await context.Messages.FirstOrDefaultAsync();
        Assert.NotNull(savedMessage);
        Assert.Equal(role, savedMessage.Role);
    }
}
