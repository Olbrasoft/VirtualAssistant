using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.Configurations;

public class LlmCorrectionConfigurationTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    private async Task<(Provider provider, LlmModel model, Prompt prompt, WhisperTranscription transcription)> SetupDependencies(VirtualAssistantDbContext context)
    {
        var provider = new Provider { Name = "Test LLM", Type = "llm", Enabled = true, Priority = 1 };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        var model = new LlmModel
        {
            Name = "Test Model",
            ModelIdentifier = "test-model",
            ProviderId = provider.Id
        };
        context.LlmModels.Add(model);

        var prompt = new Prompt
        {
            Name = "Test Prompt",
            ApplicationName = "Test",
            AppIdPattern = "*",
            PromptFileName = "test.md"
        };
        context.Prompts.Add(prompt);

        var transcription = new WhisperTranscription
        {
            TranscribedText = "Test transcription",
            AudioDurationMs = 1000
        };
        context.WhisperTranscriptions.Add(transcription);

        await context.SaveChangesAsync();

        return (provider, model, prompt, transcription);
    }

    [Fact]
    public async Task LlmCorrection_WithModelId_CanBeSavedAndRetrieved()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, model, prompt, transcription) = await SetupDependencies(context);

        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = "Corrected text",
            DurationMs = 50,
            PromptId = prompt.Id,
            ModelId = model.Id
        };

        // Act
        context.LlmCorrections.Add(correction);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.LlmCorrections.FirstAsync();
        Assert.Equal(model.Id, saved.ModelId);
        Assert.Equal("Corrected text", saved.CorrectedText);
    }

    [Fact]
    public async Task LlmCorrection_WithNullModelId_CanBeSaved()
    {
        // Arrange - simulates legacy corrections before model tracking
        using var context = CreateInMemoryContext();
        var (_, _, prompt, transcription) = await SetupDependencies(context);

        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = "Legacy corrected text",
            DurationMs = 50,
            PromptId = prompt.Id,
            ModelId = null // Legacy correction without model tracking
        };

        // Act
        context.LlmCorrections.Add(correction);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.LlmCorrections.FirstAsync();
        Assert.Null(saved.ModelId);
    }

    [Fact]
    public async Task LlmCorrection_CanNavigateToModel()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, model, prompt, transcription) = await SetupDependencies(context);

        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = transcription.Id,
            CorrectedText = "Corrected text",
            DurationMs = 50,
            PromptId = prompt.Id,
            ModelId = model.Id
        };
        context.LlmCorrections.Add(correction);
        await context.SaveChangesAsync();

        // Act
        var saved = await context.LlmCorrections
            .Include(c => c.Model)
            .FirstAsync();

        // Assert
        Assert.NotNull(saved.Model);
        Assert.Equal("Test Model", saved.Model.Name);
        Assert.Equal("test-model", saved.Model.ModelIdentifier);
    }

    [Fact]
    public async Task LlmCorrection_CanQueryByModelId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (provider, model1, prompt, transcription) = await SetupDependencies(context);

        var model2 = new LlmModel
        {
            Name = "Model 2",
            ModelIdentifier = "model-2",
            ProviderId = provider.Id
        };
        context.LlmModels.Add(model2);
        await context.SaveChangesAsync();

        var transcription2 = new WhisperTranscription
        {
            TranscribedText = "Another transcription",
            AudioDurationMs = 1000
        };
        context.WhisperTranscriptions.Add(transcription2);
        await context.SaveChangesAsync();

        context.LlmCorrections.AddRange(
            new LlmCorrection
            {
                WhisperTranscriptionId = transcription.Id,
                CorrectedText = "By Model 1",
                DurationMs = 50,
                PromptId = prompt.Id,
                ModelId = model1.Id
            },
            new LlmCorrection
            {
                WhisperTranscriptionId = transcription2.Id,
                CorrectedText = "By Model 2",
                DurationMs = 60,
                PromptId = prompt.Id,
                ModelId = model2.Id
            }
        );
        await context.SaveChangesAsync();

        // Act
        var model1Corrections = await context.LlmCorrections
            .Where(c => c.ModelId == model1.Id)
            .ToListAsync();

        // Assert
        Assert.Single(model1Corrections);
        Assert.Equal("By Model 1", model1Corrections[0].CorrectedText);
    }

    [Fact]
    public async Task LlmModel_CanNavigateToLlmCorrections()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, model, prompt, transcription) = await SetupDependencies(context);

        context.LlmCorrections.AddRange(
            new LlmCorrection
            {
                WhisperTranscriptionId = transcription.Id,
                CorrectedText = "Correction 1",
                DurationMs = 50,
                PromptId = prompt.Id,
                ModelId = model.Id
            },
            new LlmCorrection
            {
                WhisperTranscriptionId = transcription.Id,
                CorrectedText = "Correction 2",
                DurationMs = 60,
                PromptId = prompt.Id,
                ModelId = model.Id
            }
        );
        await context.SaveChangesAsync();

        // Act
        var savedModel = await context.LlmModels
            .Include(m => m.LlmCorrections)
            .FirstAsync();

        // Assert
        Assert.Equal(2, savedModel.LlmCorrections.Count);
    }

    [Fact]
    public async Task LlmCorrection_CanFilterLegacyCorrections()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (_, model, prompt, transcription) = await SetupDependencies(context);

        var transcription2 = new WhisperTranscription
        {
            TranscribedText = "Another",
            AudioDurationMs = 1000
        };
        context.WhisperTranscriptions.Add(transcription2);
        await context.SaveChangesAsync();

        context.LlmCorrections.AddRange(
            new LlmCorrection
            {
                WhisperTranscriptionId = transcription.Id,
                CorrectedText = "New correction with model",
                DurationMs = 50,
                PromptId = prompt.Id,
                ModelId = model.Id
            },
            new LlmCorrection
            {
                WhisperTranscriptionId = transcription2.Id,
                CorrectedText = "Legacy correction",
                DurationMs = 60,
                PromptId = prompt.Id,
                ModelId = null // Legacy
            }
        );
        await context.SaveChangesAsync();

        // Act
        var legacyCorrections = await context.LlmCorrections
            .Where(c => c.ModelId == null)
            .ToListAsync();

        var newCorrections = await context.LlmCorrections
            .Where(c => c.ModelId != null)
            .ToListAsync();

        // Assert
        Assert.Single(legacyCorrections);
        Assert.Single(newCorrections);
        Assert.Equal("Legacy correction", legacyCorrections[0].CorrectedText);
    }
}
