using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.Configurations;

public class LlmModelConfigurationTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    [Fact]
    public async Task LlmModel_CanBeSavedAndRetrieved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider
        {
            Name = "Test Provider",
            Type = "llm",
            Enabled = true,
            Priority = 1
        };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Test Model",
            ModelIdentifier = "test-model-v1",
            ProviderId = provider.Id,
            IsActive = true
        };

        // Act
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.LlmModels.FirstAsync();
        Assert.Equal("Test Model", saved.Name);
        Assert.Equal("test-model-v1", saved.ModelIdentifier);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task LlmModel_AutoGeneratesId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Provider", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Model",
            ModelIdentifier = "model-id",
            ProviderId = provider.Id
        };

        // Act
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        // Assert
        Assert.True(model.Id > 0);
    }

    [Fact]
    public async Task LlmModel_CanNavigateToProvider()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider
        {
            Name = "Mistral AI",
            Type = "llm",
            Enabled = true,
            Priority = 1
        };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Mistral Large",
            ModelIdentifier = "mistral-large-latest",
            ProviderId = provider.Id
        };
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        // Act
        var savedModel = await context.LlmModels
            .Include(m => m.Provider)
            .FirstAsync();

        // Assert
        Assert.NotNull(savedModel.Provider);
        Assert.Equal("Mistral AI", savedModel.Provider.Name);
    }

    [Fact]
    public async Task LlmModel_IsActiveDefaultsToTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Provider", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Model",
            ModelIdentifier = "model-id",
            ProviderId = provider.Id
            // IsActive not explicitly set
        };

        // Act
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.LlmModels.FirstAsync();
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task LlmModel_CanBeDeactivated()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Provider", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Deprecated Model",
            ModelIdentifier = "old-model",
            ProviderId = provider.Id,
            IsActive = false
        };

        // Act
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.LlmModels.FirstAsync();
        Assert.False(saved.IsActive);
    }

    [Fact]
    public async Task LlmModel_CanQueryByModelIdentifier()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Provider", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        context.LlmModels.AddRange(
            new LlmModel { Name = "Model A", ModelIdentifier = "model-a", ProviderId = provider.Id },
            new LlmModel { Name = "Model B", ModelIdentifier = "model-b", ProviderId = provider.Id }
        );
        await context.SaveChangesAsync();

        // Act
        var found = await context.LlmModels
            .Where(m => m.ModelIdentifier == "model-b")
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(found);
        Assert.Equal("Model B", found.Name);
    }

    [Fact]
    public async Task LlmModel_CanQueryActiveModels()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Provider", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        context.LlmModels.AddRange(
            new LlmModel { Name = "Active Model", ModelIdentifier = "active", ProviderId = provider.Id, IsActive = true },
            new LlmModel { Name = "Inactive Model", ModelIdentifier = "inactive", ProviderId = provider.Id, IsActive = false }
        );
        await context.SaveChangesAsync();

        // Act
        var activeModels = await context.LlmModels
            .Where(m => m.IsActive)
            .ToListAsync();

        // Assert
        Assert.Single(activeModels);
        Assert.Equal("Active Model", activeModels[0].Name);
    }

    [Fact]
    public async Task LlmModel_SetsCreatedAtAutomatically()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var provider = new Provider { Name = "Provider", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var beforeCreate = DateTime.UtcNow;
        var model = new LlmModel
        {
            Name = "Model",
            ModelIdentifier = "model-id",
            ProviderId = provider.Id
        };

        // Act
        context.LlmModels.Add(model);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.LlmModels.FirstAsync();
        Assert.True(saved.CreatedAt >= beforeCreate);
        Assert.True(saved.CreatedAt <= DateTime.UtcNow);
    }
}
