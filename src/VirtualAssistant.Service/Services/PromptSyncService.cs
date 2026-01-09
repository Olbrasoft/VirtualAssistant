using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Service.Configuration;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Service for synchronizing LLM prompt files from source to deployment directory.
/// </summary>
public class PromptSyncService : IPromptSyncService
{
    private readonly ILogger<PromptSyncService> _logger;
    private readonly string _sourcePath;
    private readonly string _targetPath;

    public PromptSyncService(
        IOptions<PromptSyncOptions> options,
        ILogger<PromptSyncService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Expand ~ to home directory
        _sourcePath = ExpandPath(opts.SourcePath);
        _targetPath = Path.Combine(AppContext.BaseDirectory, "Prompts");

        _logger.LogInformation("PromptSyncService initialized. Source: {Source}, Target: {Target}",
            _sourcePath, _targetPath);
    }

    /// <inheritdoc/>
    public string SourcePath => _sourcePath;

    /// <inheritdoc/>
    public string TargetPath => _targetPath;

    /// <inheritdoc/>
    public bool ArePromptsOutOfSync()
    {
        try
        {
            if (!Directory.Exists(_sourcePath))
            {
                _logger.LogWarning("Source directory does not exist: {Path}", _sourcePath);
                return false;
            }

            if (!Directory.Exists(_targetPath))
            {
                _logger.LogDebug("Target directory does not exist, prompts are out of sync");
                return true;
            }

            var sourceFiles = Directory.GetFiles(_sourcePath, "*.md");

            foreach (var sourceFile in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var targetFile = Path.Combine(_targetPath, fileName);

                // New file in source
                if (!File.Exists(targetFile))
                {
                    _logger.LogDebug("New prompt file in source: {File}", fileName);
                    return true;
                }

                // Source is newer than target
                var sourceTime = File.GetLastWriteTimeUtc(sourceFile);
                var targetTime = File.GetLastWriteTimeUtc(targetFile);

                if (sourceTime > targetTime)
                {
                    _logger.LogDebug("Prompt file modified: {File} (source: {SourceTime}, target: {TargetTime})",
                        fileName, sourceTime, targetTime);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking prompt sync status");
            return false;
        }
    }

    /// <inheritdoc/>
    public PromptSyncResult SyncPrompts()
    {
        var errors = new List<string>();
        var copied = 0;
        var failed = 0;

        try
        {
            // Validate source directory
            if (!Directory.Exists(_sourcePath))
            {
                var error = $"Source directory does not exist: {_sourcePath}";
                _logger.LogError(error);
                return new PromptSyncResult(false, 0, 0, [error]);
            }

            // Get source files
            var sourceFiles = Directory.GetFiles(_sourcePath, "*.md");

            if (sourceFiles.Length == 0)
            {
                var error = $"No prompt files (*.md) found in source directory: {_sourcePath}";
                _logger.LogWarning(error);
                return new PromptSyncResult(false, 0, 0, [error]);
            }

            // Ensure target directory exists
            if (!Directory.Exists(_targetPath))
            {
                try
                {
                    Directory.CreateDirectory(_targetPath);
                    _logger.LogInformation("Created target directory: {Path}", _targetPath);
                }
                catch (Exception ex)
                {
                    var error = $"Failed to create target directory: {ex.Message}";
                    _logger.LogError(ex, error);
                    return new PromptSyncResult(false, 0, 0, [error]);
                }
            }

            // Copy each file
            foreach (var sourceFile in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var targetFile = Path.Combine(_targetPath, fileName);

                try
                {
                    File.Copy(sourceFile, targetFile, overwrite: true);
                    copied++;
                    _logger.LogDebug("Copied prompt: {File}", fileName);
                }
                catch (Exception ex)
                {
                    failed++;
                    var error = $"Failed to copy {fileName}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, "Failed to copy prompt file: {File}", fileName);
                }
            }

            // Validate copy
            if (failed > 0)
            {
                _logger.LogWarning("Prompt sync completed with errors. Copied: {Copied}, Failed: {Failed}",
                    copied, failed);
                return new PromptSyncResult(false, copied, failed, errors);
            }

            _logger.LogInformation("Prompt sync completed successfully. Copied {Count} files.", copied);
            return new PromptSyncResult(true, copied, 0, []);
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error during prompt sync: {ex.Message}";
            _logger.LogError(ex, error);
            errors.Add(error);
            return new PromptSyncResult(false, copied, failed, errors);
        }
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return path;
    }
}
