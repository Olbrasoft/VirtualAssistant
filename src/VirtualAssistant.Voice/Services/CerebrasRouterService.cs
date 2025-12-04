using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Cerebras LLM router service implementation.
/// Uses Cerebras Inference API (OpenAI-compatible).
/// </summary>
public class CerebrasRouterService : BaseLlmRouterService
{
    public override string ProviderName => "Cerebras";
    public override LlmProvider Provider => LlmProvider.Cerebras;

    public CerebrasRouterService(
        ILogger<CerebrasRouterService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
        : base(logger, httpClient, GetModel(configuration))
    {
        // Try environment variable first, then config
        var apiKey = Environment.GetEnvironmentVariable("CEREBRAS_API_KEY") 
                  ?? configuration["CerebrasRouter:ApiKey"] 
                  ?? "";
        
        httpClient.BaseAddress = new Uri("https://api.cerebras.ai/v1/");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        var hasKey = !string.IsNullOrEmpty(apiKey);
        logger.LogInformation("Cerebras Router initialized with model {Model}, API key: {HasKey}", _model, hasKey ? "configured" : "MISSING");
    }

    private static string GetModel(IConfiguration configuration)
    {
        return configuration["CerebrasRouter:Model"] ?? "llama-3.3-70b";
    }
}
