using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.LlmChain.Configuration;
using Olbrasoft.VirtualAssistant.LlmChain.Dtos;

namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// Builds HTTP requests for LLM API calls.
/// </summary>
public class LlmRequestBuilder : ILlmRequestBuilder
{
    private readonly ILogger<LlmRequestBuilder> _logger;
    private readonly LlmChainOptions _options;

    public LlmRequestBuilder(
        ILogger<LlmRequestBuilder> logger,
        IOptions<LlmChainOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public void ConfigureHttpClient(HttpClient httpClient, LlmProviderConfig provider, string apiKey)
    {
        httpClient.BaseAddress = new Uri(provider.BaseUrl);
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        httpClient.Timeout = _options.RequestTimeout;
    }

    /// <inheritdoc />
    public StringContent BuildRequestContent(LlmChainRequest request, LlmProviderConfig provider)
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
        _logger.LogDebug("Built LLM request for {Provider}: {Model}", provider.Name, provider.Model);

        return new StringContent(requestJson, Encoding.UTF8, "application/json");
    }
}
