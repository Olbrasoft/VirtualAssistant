using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.Testing.Xunit.Attributes;
using VirtualAssistant.LlmChain;
using VirtualAssistant.LlmChain.Configuration;
using Xunit.Abstractions;

namespace VirtualAssistant.LlmChain.IntegrationTests;

/// <summary>
/// Integration tests for LlmChainClient.
/// These tests call real LLM APIs and are skipped on CI environments.
/// Uses [SkipOnCIFact] to automatically skip on GitHub Actions, Azure DevOps, etc.
/// </summary>
public class LlmChainIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILlmChainClient _client;
    private readonly LlmChainOptions _options;

    public LlmChainIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetProjectRoot())
            .AddJsonFile("appsettings.integrationtests.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(output));
        services.AddLlmChain(configuration);

        var provider = services.BuildServiceProvider();
        _client = provider.GetRequiredService<ILlmChainClient>();

        _options = new LlmChainOptions();
        configuration.GetSection(LlmChainOptions.SectionName).Bind(_options);
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

    [Fact(Skip = "Disabled to save API credits - run manually when needed")]
    public async Task CompleteAsync_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new LlmChainRequest
        {
            SystemPrompt = "You are a helpful assistant. Respond in one short sentence.",
            UserMessage = "Say hello in Czech.",
            Temperature = 0.3f,
            MaxTokens = 50
        };

        // Act
        var result = await _client.CompleteAsync(request);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content);
        Assert.NotNull(result.ProviderName);

        _output.WriteLine($"Provider: {result.ProviderName}");
        _output.WriteLine($"Key: {result.KeyIdentifier}");
        _output.WriteLine($"Response time: {result.ResponseTimeMs}ms");
        _output.WriteLine($"Content: {result.Content}");
    }

    [Fact(Skip = "Disabled to save API credits - run manually when needed")]
    public async Task CompleteAsync_MistralProvider_Works()
    {
        // Test specifically with Mistral
        await TestProvider("Mistral");
    }

    [Fact(Skip = "Disabled to save API credits - run manually when needed")]
    public async Task CompleteAsync_CerebrasProvider_Works()
    {
        // Test specifically with Cerebras
        await TestProvider("Cerebras");
    }

    [Fact(Skip = "Disabled to save API credits - run manually when needed")]
    public async Task CompleteAsync_GroqProvider_Works()
    {
        // Test specifically with Groq
        await TestProvider("Groq");
    }

    [Fact(Skip = "Disabled to save API credits - run manually when needed")]
    public async Task CompleteAsync_OpenRouterProvider_Works()
    {
        // Test specifically with OpenRouter
        await TestProvider("OpenRouter");
    }

    private async Task TestProvider(string providerName)
    {
        // Create a client with only one provider enabled
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetProjectRoot())
            .AddJsonFile("appsettings.integrationtests.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output));

        // Configure with only the specific provider enabled
        services.AddLlmChain(options =>
        {
            configuration.GetSection(LlmChainOptions.SectionName).Bind(options);

            // Disable all providers except the one we're testing
            foreach (var provider in options.Providers)
            {
                provider.Enabled = provider.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase);
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

        var result = await client.CompleteAsync(request);

        _output.WriteLine($"Testing {providerName}:");
        _output.WriteLine($"  Success: {result.Success}");
        _output.WriteLine($"  Provider: {result.ProviderName}");
        _output.WriteLine($"  Key: {result.KeyIdentifier}");
        _output.WriteLine($"  Time: {result.ResponseTimeMs}ms");
        _output.WriteLine($"  Content: {result.Content}");

        if (!result.Success)
        {
            _output.WriteLine($"  Error: {result.Error}");
            foreach (var attempt in result.Attempts)
            {
                _output.WriteLine($"  Attempt: {attempt.Provider} ({attempt.KeyId}): {attempt.Error}");
            }
        }

        Assert.True(result.Success, $"{providerName} failed: {result.Error}");
        Assert.Equal(providerName, result.ProviderName);
        Assert.NotNull(result.Content);
    }

    [Fact(Skip = "Disabled to save API credits - run manually when needed")]
    public async Task CompleteAsync_AllProvidersRotation_UsesMultipleProviders()
    {
        // Send multiple requests and verify different providers are used (round-robin)
        var usedProviders = new HashSet<string>();
        var request = new LlmChainRequest
        {
            SystemPrompt = "Respond with just 'OK'.",
            UserMessage = "Test",
            Temperature = 0.1f,
            MaxTokens = 10
        };

        // Make several requests to trigger rotation
        for (int i = 0; i < 4; i++)
        {
            var result = await _client.CompleteAsync(request);
            if (result.Success && result.ProviderName != null)
            {
                usedProviders.Add(result.ProviderName);
                _output.WriteLine($"Request {i + 1}: {result.ProviderName} ({result.KeyIdentifier})");
            }

            // Small delay to avoid rate limiting
            await Task.Delay(500);
        }

        _output.WriteLine($"Used providers: {string.Join(", ", usedProviders)}");

        // At least 2 different providers should be used (round-robin)
        Assert.True(usedProviders.Count >= 2,
            $"Expected at least 2 providers used, but only got: {string.Join(", ", usedProviders)}");
    }
}

/// <summary>
/// Extension method to add xUnit logging to ILoggingBuilder.
/// </summary>
public static class LoggingExtensions
{
    public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output)
    {
        builder.AddProvider(new XUnitLoggerProvider(output));
        builder.SetMinimumLevel(LogLevel.Debug);
        return builder;
    }
}

public class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XUnitLoggerProvider(ITestOutputHelper output) => _output = output;

    public ILogger CreateLogger(string categoryName) => new XUnitLogger(_output, categoryName);

    public void Dispose() { }
}

public class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XUnitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
        }
        catch
        {
            // Ignore - test might have ended
        }
    }
}
