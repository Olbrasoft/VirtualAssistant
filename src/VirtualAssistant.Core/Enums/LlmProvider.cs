namespace Olbrasoft.VirtualAssistant.Core.Enums;

/// <summary>
/// Available LLM providers for routing
/// </summary>
public enum LlmProvider
{
    /// <summary>
    /// Mistral AI - primary provider, most reliable
    /// </summary>
    Mistral = 0,

    /// <summary>
    /// Groq - fast inference, 100k daily token limit
    /// </summary>
    Groq = 1,

    /// <summary>
    /// Cerebras - alternative provider
    /// </summary>
    Cerebras = 2
}
