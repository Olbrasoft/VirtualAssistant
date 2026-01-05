using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Hybrid prompt loader that tries to load prompts from file system first,
/// then falls back to embedded resources if file not found.
/// This enables hot-reloading of prompts without application restart.
/// </summary>
public class HybridPromptLoader : IPromptLoader
{
    private readonly string _fileBasePath;
    private readonly IPromptLoader _embeddedFallback;
    private readonly ILogger<HybridPromptLoader> _logger;

    public HybridPromptLoader(
        string fileBasePath,
        IPromptLoader embeddedFallback,
        ILogger<HybridPromptLoader> logger)
    {
        _fileBasePath = fileBasePath ?? throw new ArgumentNullException(nameof(fileBasePath));
        _embeddedFallback = embeddedFallback ?? throw new ArgumentNullException(nameof(embeddedFallback));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads a prompt, trying file system first and falling back to embedded resource.
    /// </summary>
    /// <param name="promptName">The name of the prompt file (without .md extension).</param>
    /// <returns>The content of the prompt file.</returns>
    /// <exception cref="ArgumentException">Thrown when promptName is null or whitespace.</exception>
    public string LoadPrompt(string promptName)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name cannot be null or whitespace.", nameof(promptName));
        }

        // Try loading from file first
        var filePath = Path.Combine(_fileBasePath, $"{promptName}.md");

        if (File.Exists(filePath))
        {
            try
            {
                var content = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Prompt file '{FilePath}' is empty, falling back to embedded resource", filePath);
                }
                else
                {
                    _logger.LogInformation("Loaded prompt '{PromptName}' from file: {FilePath}", promptName, filePath);
                    return content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load prompt '{PromptName}' from file '{FilePath}', falling back to embedded resource",
                    promptName, filePath);
            }
        }
        else
        {
            _logger.LogDebug("Prompt file '{FilePath}' not found, using embedded resource", filePath);
        }

        // Fallback to embedded resource
        var embeddedContent = _embeddedFallback.LoadPrompt(promptName);
        _logger.LogInformation("Loaded prompt '{PromptName}' from embedded resource", promptName);
        return embeddedContent;
    }

    /// <summary>
    /// Loads a prompt and replaces placeholders with provided values.
    /// Tries file system first, falls back to embedded resource.
    /// </summary>
    /// <param name="promptName">The name of the prompt file (without .md extension).</param>
    /// <param name="values">Dictionary of placeholder keys and their replacement values.</param>
    /// <returns>The prompt content with placeholders replaced.</returns>
    public string LoadPromptWithValues(string promptName, Dictionary<string, string> values)
    {
        var template = LoadPrompt(promptName);

        foreach (var (key, value) in values)
        {
            template = template.Replace($"{{{{{key}}}}}", value);
        }

        return template;
    }
}
