using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Mistral LLM router service implementation.
/// Uses Mistral AI API (OpenAI-compatible).
/// </summary>
public class MistralRouterService : BaseLlmRouterService
{
    public override string ProviderName => "Mistral";
    public override LlmProvider Provider => LlmProvider.Mistral;

    public MistralRouterService(
        ILogger<MistralRouterService> logger,
        HttpClient httpClient,
        IConfiguration configuration,
        IPromptLoader promptLoader)
        : base(logger, httpClient, GetModel(configuration), promptLoader)
    {
        // Try environment variable first, then config, then file
        var apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
                  ?? configuration["MistralRouter:ApiKey"]
                  ?? LoadApiKeyFromFile(configuration["MistralRouter:ApiKeyFile"])
                  ?? "";

        httpClient.BaseAddress = new Uri("https://api.mistral.ai/v1/");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var hasKey = !string.IsNullOrEmpty(apiKey);
        logger.LogInformation("Mistral Router initialized with model {Model}, API key: {HasKey}", _model, hasKey ? "configured" : "MISSING");
    }

    private static string? LoadApiKeyFromFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var path = filePath.StartsWith("~")
            ? filePath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            : filePath;

        if (!File.Exists(path))
            return null;

        return File.ReadAllLines(path)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            ?.Trim();
    }

    private static string GetModel(IConfiguration configuration)
    {
        return configuration["MistralRouter:Model"] ?? "mistral-small-latest";
    }
}
