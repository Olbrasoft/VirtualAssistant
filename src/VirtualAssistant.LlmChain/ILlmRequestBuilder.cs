using Olbrasoft.VirtualAssistant.LlmChain.Configuration;

namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// Builds HTTP requests for LLM API calls.
/// </summary>
public interface ILlmRequestBuilder
{
    /// <summary>
    /// Builds a complete HTTP request message for an LLM API call.
    /// Returns a fully configured HttpRequestMessage that can be sent via HttpClient.
    /// This approach avoids modifying shared/pooled HttpClient instances.
    /// </summary>
    /// <param name="request">The LLM chain request</param>
    /// <param name="provider">The LLM provider configuration</param>
    /// <param name="apiKey">The API key to use for authentication</param>
    /// <returns>Configured HttpRequestMessage ready to send</returns>
    HttpRequestMessage BuildRequest(LlmChainRequest request, LlmProviderConfig provider, string apiKey);
}
