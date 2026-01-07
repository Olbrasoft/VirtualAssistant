using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.LlmChain.Configuration;
using Olbrasoft.VirtualAssistant.LlmChain.Dtos;

namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// Builds HTTP requests for LLM API calls.
/// Creates fully configured HttpRequestMessage instances to avoid
/// modifying shared/pooled HttpClient instances from IHttpClientFactory.
/// </summary>
public class LlmRequestBuilder : ILlmRequestBuilder
{
    private readonly ILogger<LlmRequestBuilder> _logger;

    public LlmRequestBuilder(ILogger<LlmRequestBuilder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public HttpRequestMessage BuildRequest(LlmChainRequest request, LlmProviderConfig provider, string apiKey)
    {
        var llmRequest = new LlmApiRequest
        {
            Model = provider.Model,
            Messages =
            [
                new LlmApiMessage { Role = "system", Content = request.SystemPrompt },
                new LlmApiMessage { Role = "user", Content = request.UserMessage }
            ],
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        var requestJson = JsonSerializer.Serialize(llmRequest);

        // Build complete request URL
        var requestUri = new Uri(new Uri(provider.BaseUrl), "chat/completions");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        // Set authorization header on the request, not the shared client
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        _logger.LogDebug("Built LLM request for {Provider}: {Model}", provider.Name, provider.Model);

        return httpRequest;
    }
}
