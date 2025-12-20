namespace Olbrasoft.VirtualAssistant.Core.Configuration;

/// <summary>
/// Locates Whisper AI models following Linux FHS (Filesystem Hierarchy Standard).
/// Searches in XDG-compliant locations with fallback to legacy paths.
/// </summary>
public static class WhisperModelLocator
{
    /// <summary>
    /// Gets the directory containing Whisper models.
    /// Priority: XDG user dir → system-wide dir → legacy dir → create XDG dir.
    /// </summary>
    /// <returns>Full path to models directory</returns>
    public static string GetModelsDirectory()
    {
        // 1. XDG-compliant user directory (preferred)
        // Linux: ~/.local/share/whisper-models
        var xdgData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var xdgModels = Path.Combine(xdgData, "whisper-models");
        if (Directory.Exists(xdgModels))
            return xdgModels;

        // 2. System-wide directory (for multi-user systems)
        const string systemModels = "/usr/share/whisper-models";
        if (Directory.Exists(systemModels))
            return systemModels;

        // 3. Legacy fallback (backwards compatibility)
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var legacyPath = Path.Combine(userProfile, "apps/asr-models");
        if (Directory.Exists(legacyPath))
            return legacyPath;

        // 4. Create XDG directory if nothing exists
        Directory.CreateDirectory(xdgModels);
        return xdgModels;
    }

    /// <summary>
    /// Gets the full path to a specific Whisper model file.
    /// </summary>
    /// <param name="modelFileName">Name of the model file (e.g., "ggml-large-v3-turbo.bin")</param>
    /// <returns>Full path to the model file</returns>
    /// <exception cref="FileNotFoundException">Model file not found in any expected location</exception>
    public static string GetModelPath(string modelFileName)
    {
        if (string.IsNullOrWhiteSpace(modelFileName))
            throw new ArgumentException("Model file name cannot be empty", nameof(modelFileName));

        var modelsDir = GetModelsDirectory();
        var fullPath = Path.Combine(modelsDir, modelFileName);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Whisper model not found: {fullPath}\n" +
                $"Expected locations:\n" +
                $"  1. ~/.local/share/whisper-models/{modelFileName}\n" +
                $"  2. /usr/share/whisper-models/{modelFileName}\n" +
                $"  3. ~/apps/asr-models/{modelFileName} (legacy)",
                fullPath
            );
        }

        return fullPath;
    }
}
