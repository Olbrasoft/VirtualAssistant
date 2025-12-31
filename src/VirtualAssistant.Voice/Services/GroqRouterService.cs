using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Groq LLM router service implementation.
/// Uses Groq API (OpenAI-compatible).
/// </summary>
public class GroqRouterService : BaseLlmRouterService
{
    public override string ProviderName => "Groq";
    public override LlmProvider Provider => LlmProvider.Groq;

    public GroqRouterService(
        ILogger<GroqRouterService> logger,
        HttpClient httpClient,
        IConfiguration configuration,
        IPromptLoader promptLoader,
        IOptions<LlmRoutingOptions> options)
        : base(logger, httpClient, GetModel(configuration), promptLoader, options)
    {
        // Try environment variable first, then config
        var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") 
                  ?? configuration["GroqRouter:ApiKey"] 
                  ?? "";
        
        httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        var hasKey = !string.IsNullOrEmpty(apiKey);
        logger.LogInformation("Groq Router initialized with model {Model}, API key: {HasKey}", _model, hasKey ? "configured" : "MISSING");
    }

    private static string GetModel(IConfiguration configuration)
    {
        return configuration["GroqRouter:Model"] ?? "llama-3.3-70b-versatile";
    }
}
