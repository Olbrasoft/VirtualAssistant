using System.Reflection;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Loads prompt templates from embedded resources in the assembly.
/// </summary>
public class EmbeddedPromptLoader : IPromptLoader
{
    private readonly Assembly _assembly;
    private readonly string _baseNamespace;

    public EmbeddedPromptLoader()
        : this(Assembly.GetExecutingAssembly(), "VirtualAssistant.Voice.Prompts")
    {
    }

    public EmbeddedPromptLoader(Assembly assembly, string baseNamespace)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _baseNamespace = baseNamespace ?? throw new ArgumentNullException(nameof(baseNamespace));
    }

    /// <summary>
    /// Loads a prompt from an embedded resource file.
    /// </summary>
    /// <param name="promptName">The name of the prompt file (without .md extension).</param>
    /// <returns>The content of the prompt file.</returns>
    /// <exception cref="ArgumentException">Thrown when promptName is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the embedded resource is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the prompt file is empty.</exception>
    public string LoadPrompt(string promptName)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name cannot be null or whitespace.", nameof(promptName));
        }

        var resourceName = $"{_baseNamespace}.{promptName}.md";

        using var stream = _assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            var availableResources = string.Join(", ", _assembly.GetManifestResourceNames());
            throw new FileNotFoundException(
                $"Embedded resource '{resourceName}' not found. Available resources: {availableResources}",
                resourceName);
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Prompt '{resourceName}' is empty or contains only whitespace.");
        }

        return content;
    }

    /// <summary>
    /// Loads a prompt from an embedded resource and replaces placeholders with provided values.
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
