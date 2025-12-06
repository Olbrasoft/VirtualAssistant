namespace VirtualAssistant.GitHub.Configuration;

/// <summary>
/// Configuration settings for embedding generation.
/// </summary>
public class EmbeddingSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Embeddings";

    /// <summary>
    /// Gets or sets the embedding provider (e.g., "Ollama", "OpenRouter").
    /// Default: "Ollama" (local).
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>
    /// Gets or sets the embedding model to use.
    /// Default: "nomic-embed-text" (768 dimensions).
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Gets or sets the embedding dimensions.
    /// Default: 768 (nomic-embed-text).
    /// </summary>
    public int Dimensions { get; set; } = 768;

    /// <summary>
    /// Gets or sets the API key for the embedding provider.
    /// Can also be loaded from file using ApiKeyFile.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to a file containing the API key.
    /// If specified and ApiKey is empty, the key will be read from this file.
    /// </summary>
    public string ApiKeyFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for the API.
    /// Default: "http://localhost:11434" for Ollama.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets whether to skip embedding generation for very short content.
    /// Default: true.
    /// </summary>
    public bool SkipShortContent { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum content length to generate embeddings.
    /// Default: 10 characters.
    /// </summary>
    public int MinContentLength { get; set; } = 10;

    /// <summary>
    /// Gets or sets the batch size for embedding requests.
    /// Default: 20.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Gets the effective API key (from ApiKey or ApiKeyFile).
    /// </summary>
    public string GetEffectiveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKey))
            return ApiKey;

        if (!string.IsNullOrWhiteSpace(ApiKeyFile))
        {
            // Expand tilde to home directory
            var expandedPath = ApiKeyFile.StartsWith("~/")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ApiKeyFile[2..])
                : ApiKeyFile;

            if (File.Exists(expandedPath))
                return File.ReadAllText(expandedPath).Trim();
        }

        return string.Empty;
    }
}
