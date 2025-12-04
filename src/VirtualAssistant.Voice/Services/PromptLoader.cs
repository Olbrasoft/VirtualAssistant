using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for loading prompt templates from markdown files.
/// Prompts are stored in the Prompts directory and loaded at runtime.
/// </summary>
public interface IPromptLoader
{
    /// <summary>
    /// Loads a prompt template from a markdown file.
    /// </summary>
    /// <param name="promptName">Name of the prompt file (without .md extension)</param>
    /// <returns>The prompt content</returns>
    string LoadPrompt(string promptName);

    /// <summary>
    /// Loads a prompt and replaces placeholders with values.
    /// Placeholders use {{PlaceholderName}} syntax.
    /// </summary>
    string LoadPromptWithValues(string promptName, Dictionary<string, string> values);
}

/// <summary>
/// Loads prompts from markdown files in the Prompts directory.
/// Files are cached after first load for performance.
/// </summary>
public class PromptLoader : IPromptLoader
{
    private readonly ILogger<PromptLoader> _logger;
    private readonly string _promptsDirectory;
    private readonly Dictionary<string, string> _cache = new();

    public PromptLoader(ILogger<PromptLoader> logger)
    {
        _logger = logger;
        
        // Find Prompts directory relative to assembly location
        var assemblyLocation = AppContext.BaseDirectory;
        _promptsDirectory = Path.Combine(assemblyLocation, "Prompts");
        
        // Fallback to development path if not found
        if (!Directory.Exists(_promptsDirectory))
        {
            var devPath = FindPromptsDirectoryInDevelopment();
            if (devPath != null)
            {
                _promptsDirectory = devPath;
            }
        }
        
        _logger.LogInformation("Prompts directory: {Directory}", _promptsDirectory);
    }

    public string LoadPrompt(string promptName)
    {
        if (_cache.TryGetValue(promptName, out var cached))
        {
            return cached;
        }

        var filePath = Path.Combine(_promptsDirectory, $"{promptName}.md");
        
        if (!File.Exists(filePath))
        {
            _logger.LogError("Prompt file not found: {FilePath}", filePath);
            throw new FileNotFoundException($"Prompt file not found: {filePath}");
        }

        var content = File.ReadAllText(filePath);
        _cache[promptName] = content;
        
        _logger.LogDebug("Loaded prompt: {PromptName} ({Length} chars)", promptName, content.Length);
        return content;
    }

    public string LoadPromptWithValues(string promptName, Dictionary<string, string> values)
    {
        var template = LoadPrompt(promptName);
        
        foreach (var (key, value) in values)
        {
            template = template.Replace($"{{{{{key}}}}}", value);
        }
        
        return template;
    }

    private string? FindPromptsDirectoryInDevelopment()
    {
        // Walk up from current directory to find src/VirtualAssistant.Voice/Prompts
        var current = Directory.GetCurrentDirectory();
        
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "src", "VirtualAssistant.Voice", "Prompts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        
        return null;
    }
}
