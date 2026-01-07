using Olbrasoft.VirtualAssistant.LlmChain.Configuration;
using Olbrasoft.VirtualAssistant.LlmChain.Dtos;

namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// Builds HTTP requests for LLM API calls.
/// </summary>
public interface ILlmRequestBuilder
{
    /// <summary>
    /// Configures an HttpClient for the specified provider.
    /// </summary>
    /// <param name="httpClient">The HttpClient to configure</param>
    /// <param name="provider">The LLM provider configuration</param>
    /// <param name="apiKey">The API key to use for authentication</param>
    void ConfigureHttpClient(HttpClient httpClient, LlmProviderConfig provider, string apiKey);

    /// <summary>
    /// Builds the JSON request body for an LLM API call.
    /// </summary>
    /// <param name="request">The LLM chain request</param>
    /// <param name="provider">The LLM provider configuration</param>
    /// <returns>StringContent with JSON body ready for HTTP POST</returns>
    StringContent BuildRequestContent(LlmChainRequest request, LlmProviderConfig provider);
}
