namespace VirtualAssistant.LlmChain.Configuration;

/// <summary>
/// Configuration options for the LLM provider chain.
/// </summary>
public class LlmChainOptions
{
    public const string SectionName = "LlmChain";

    /// <summary>
    /// List of provider configurations in priority order.
    /// </summary>
    public List<LlmProviderConfig> Providers { get; set; } = [];

    /// <summary>
    /// How long to wait before retrying a provider after rate limiting (default 5 minutes).
    /// </summary>
    public TimeSpan RateLimitCooldown { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Request timeout for individual API calls.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>
/// Configuration for a single LLM provider.
/// </summary>
public class LlmProviderConfig
{
    /// <summary>
    /// Unique name for this provider (e.g., "Mistral", "Cerebras", "Groq").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Base URL for the API (e.g., "https://api.mistral.ai/v1/").
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Model to use (e.g., "mistral-small-latest", "qwen-3-32b").
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// List of API keys for this provider (for key rotation).
    /// Keys can be specified inline here or loaded from ApiKeysFile.
    /// </summary>
    public List<string> ApiKeys { get; set; } = [];

    /// <summary>
    /// Optional file path containing API keys (one per line).
    /// Supports ~ for home directory expansion.
    /// If specified, keys from this file are used instead of ApiKeys list.
    /// </summary>
    public string? ApiKeysFile { get; set; }

    /// <summary>
    /// Whether this provider is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets the effective API keys, loading from file if ApiKeysFile is specified.
    /// </summary>
    public List<string> GetEffectiveApiKeys()
    {
        if (string.IsNullOrEmpty(ApiKeysFile))
            return ApiKeys;

        var path = ApiKeysFile.StartsWith("~")
            ? ApiKeysFile.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            : ApiKeysFile;

        if (!File.Exists(path))
            return ApiKeys;

        return File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Trim())
            .ToList();
    }
}
