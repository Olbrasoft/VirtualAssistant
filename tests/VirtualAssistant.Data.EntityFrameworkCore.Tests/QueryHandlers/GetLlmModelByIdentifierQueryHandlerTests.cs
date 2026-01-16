using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmModelQueries;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.LlmModelQueryHandlers;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.QueryHandlers;

public class GetLlmModelByIdentifierQueryHandlerTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_WithExistingActiveModel_ReturnsModel()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Zen", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Alpha GLM 4.7",
            ModelIdentifier = "alpha-glm-4.7",
            ProviderId = provider.Id,
            IsActive = true
        };
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        var handler = new GetLlmModelByIdentifierQueryHandler(context);
        var query = new GetLlmModelByIdentifierQuery("alpha-glm-4.7");

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alpha GLM 4.7", result.Name);
        Assert.Equal("alpha-glm-4.7", result.ModelIdentifier);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveModel_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Zen", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Alpha GLM 4.7",
            ModelIdentifier = "alpha-glm-4.7",
            ProviderId = provider.Id,
            IsActive = false // Inactive
        };
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        var handler = new GetLlmModelByIdentifierQueryHandler(context);
        var query = new GetLlmModelByIdentifierQuery("alpha-glm-4.7");

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistingModel_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new GetLlmModelByIdentifierQueryHandler(context);
        var query = new GetLlmModelByIdentifierQuery("non-existing-model");

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleModels_ReturnsCorrectOne()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "LLM Provider", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        context.LlmModels.AddRange(
            new LlmModel
            {
                Name = "Mistral Large",
                ModelIdentifier = "mistral-large-latest",
                ProviderId = provider.Id,
                IsActive = true
            },
            new LlmModel
            {
                Name = "Alpha GLM 4.7",
                ModelIdentifier = "alpha-glm-4.7",
                ProviderId = provider.Id,
                IsActive = true
            }
        );
        await context.SaveChangesAsync();

        var handler = new GetLlmModelByIdentifierQueryHandler(context);
        var query = new GetLlmModelByIdentifierQuery("mistral-large-latest");

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mistral Large", result.Name);
    }
}
