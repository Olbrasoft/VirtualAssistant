using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.Commands;
using VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.CommandHandlers;

public class ConversationSaveCommandHandlerTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task Handle_NewConversation_CreatesAndReturnsId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new ConversationSaveCommandHandler(context);
        var command = new ConversationSaveCommand { Title = "Test Conversation" };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result > 0);
        var savedConversation = await context.Conversations.FirstOrDefaultAsync();
        Assert.NotNull(savedConversation);
        Assert.Equal("Test Conversation", savedConversation.Title);
    }

    [Fact]
    public async Task Handle_ExistingConversation_UpdatesTitle()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var existingConversation = new Conversation
        {
            Title = "Original Title",
            CreatedAt = DateTime.UtcNow
        };
        context.Conversations.Add(existingConversation);
        await context.SaveChangesAsync();

        var handler = new ConversationSaveCommandHandler(context);
        var command = new ConversationSaveCommand
        {
            Id = existingConversation.Id,
            Title = "Updated Title"
        };

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(existingConversation.Id, result);
        var updatedConversation = await context.Conversations.FirstOrDefaultAsync(c => c.Id == existingConversation.Id);
        Assert.NotNull(updatedConversation);
        Assert.Equal("Updated Title", updatedConversation.Title);
        Assert.NotNull(updatedConversation.UpdatedAt);
    }

    [Fact]
    public async Task Handle_NewConversation_SetsCreatedAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new ConversationSaveCommandHandler(context);
        var beforeCreation = DateTime.UtcNow;
        var command = new ConversationSaveCommand { Title = "Test" };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var conversation = await context.Conversations.FirstOrDefaultAsync();
        Assert.NotNull(conversation);
        Assert.True(conversation.CreatedAt >= beforeCreation);
    }
}
