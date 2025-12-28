using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.Testing.Xunit.Attributes;
using VirtualAssistant.LlmChain;
using VirtualAssistant.LlmChain.Configuration;
using Xunit.Abstractions;

namespace VirtualAssistant.LlmChain.IntegrationTests;

/// <summary>
/// Integration tests for LLM Router multi-provider fallback.
/// Tests the routing logic: Mistral → Groq → Cerebras → OpenRouter.
/// These tests call real LLM APIs and are skipped on CI environments.
/// </summary>
public class LlmRouterTests
{
    private readonly ITestOutputHelper _output;

    public LlmRouterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "appsettings.integrationtests.json")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? Directory.GetCurrentDirectory();
    }

    [SkipOnCIFact]
    public async Task CompleteAsync_MistralFailover_FallsBackToGroq()
    {
        // Arrange - Configure with Mistral disabled, Groq enabled
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetProjectRoot())
            .AddJsonFile("appsettings.integrationtests.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output));

        services.AddLlmChain(options =>
        {
            configuration.GetSection(LlmChainOptions.SectionName).Bind(options);

            // Disable Mistral to force failover to Groq
            foreach (var provider in options.Providers)
            {
                if (provider.Name.Equals("Mistral", StringComparison.OrdinalIgnoreCase))
                {
                    provider.Enabled = false;
                }
            }
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILlmChainClient>();

        var request = new LlmChainRequest
        {
            SystemPrompt = "You are a helpful assistant. Respond in one short sentence in Czech.",
            UserMessage = "What is 2+2?",
            Temperature = 0.1f,
            MaxTokens = 50
        };

        // Act
        var result = await client.CompleteAsync(request);

        // Assert
        _output.WriteLine($"Provider used: {result.ProviderName}");
        _output.WriteLine($"Key: {result.KeyIdentifier}");
        _output.WriteLine($"Response time: {result.ResponseTimeMs}ms");
        _output.WriteLine($"Content: {result.Content}");

        if (!result.Success)
        {
            _output.WriteLine($"Error: {result.Error}");
            foreach (var attempt in result.Attempts)
            {
                _output.WriteLine($"Attempt: {attempt.Provider} ({attempt.KeyId}): {attempt.Error}");
            }
        }

        Assert.True(result.Success, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content);

        // Verify failover happened - should be Groq (or Cerebras/OpenRouter if Groq is also down)
        Assert.NotEqual("Mistral", result.ProviderName);
        _output.WriteLine($"✅ Failover successful: Mistral → {result.ProviderName}");
    }

    [SkipOnCIFact]
    public async Task CompleteAsync_MultiProviderRotation_UsesRoundRobin()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetProjectRoot())
            .AddJsonFile("appsettings.integrationtests.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output));
        services.AddLlmChain(configuration);

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILlmChainClient>();

        var request = new LlmChainRequest
        {
            SystemPrompt = "Respond with just 'OK'.",
            UserMessage = "Test",
            Temperature = 0.1f,
            MaxTokens = 10
        };

        // Act - Send multiple requests to trigger rotation
        var usedProviders = new HashSet<string>();
        for (int i = 0; i < 4; i++)
        {
            var result = await client.CompleteAsync(request);
            if (result.Success && result.ProviderName != null)
            {
                usedProviders.Add(result.ProviderName);
                _output.WriteLine($"Request {i + 1}: {result.ProviderName} ({result.KeyIdentifier})");
            }

            // Small delay to avoid rate limiting
            await Task.Delay(500);
        }

        // Assert
        _output.WriteLine($"Used providers: {string.Join(", ", usedProviders)}");
        Assert.True(usedProviders.Count >= 2,
            $"Expected at least 2 providers used (round-robin), but only got: {string.Join(", ", usedProviders)}");

        _output.WriteLine($"✅ Round-robin working: {usedProviders.Count} providers used");
    }

    [SkipOnCIFact]
    public async Task CompleteAsync_AllProvidersDisabled_ReturnsError()
    {
        // Arrange - Configure with all providers disabled
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetProjectRoot())
            .AddJsonFile("appsettings.integrationtests.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output));

        services.AddLlmChain(options =>
        {
            configuration.GetSection(LlmChainOptions.SectionName).Bind(options);

            // Disable all providers
            foreach (var provider in options.Providers)
            {
                provider.Enabled = false;
            }
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILlmChainClient>();

        var request = new LlmChainRequest
        {
            SystemPrompt = "Test",
            UserMessage = "Test",
            Temperature = 0.1f,
            MaxTokens = 10
        };

        // Act
        var result = await client.CompleteAsync(request);

        // Assert
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Error: {result.Error}");

        Assert.False(result.Success, "Expected failure when all providers are disabled");
        Assert.NotNull(result.Error);
        _output.WriteLine($"✅ Correctly failed when no providers available");
    }
}
