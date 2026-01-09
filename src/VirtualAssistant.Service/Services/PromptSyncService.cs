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

        // Validate SourcePath is configured
        if (string.IsNullOrWhiteSpace(opts.SourcePath))
        {
            throw new ArgumentException("PromptSync:SourcePath must be configured", nameof(options));
        }

        // Expand ~ to home directory and validate
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
            var sourceFileNames = sourceFiles.Select(Path.GetFileName).ToHashSet();

            // Check for new or modified files in source
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

            // Check for deleted files (exist in target but not in source)
            var targetFiles = Directory.GetFiles(_targetPath, "*.md");
            foreach (var targetFile in targetFiles)
            {
                var fileName = Path.GetFileName(targetFile);
                if (!sourceFileNames.Contains(fileName))
                {
                    _logger.LogDebug("Obsolete prompt file in target (deleted from source): {File}", fileName);
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
        var removed = 0;

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
            var sourceFileNames = sourceFiles.Select(Path.GetFileName).ToHashSet();

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

            // Copy each file from source
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

            // Remove obsolete files from target (files that no longer exist in source)
            var targetFiles = Directory.GetFiles(_targetPath, "*.md");
            foreach (var targetFile in targetFiles)
            {
                var fileName = Path.GetFileName(targetFile);
                if (!sourceFileNames.Contains(fileName))
                {
                    try
                    {
                        File.Delete(targetFile);
                        removed++;
                        _logger.LogInformation("Removed obsolete prompt: {File}", fileName);
                    }
                    catch (Exception ex)
                    {
                        var error = $"Failed to remove obsolete {fileName}: {ex.Message}";
                        errors.Add(error);
                        _logger.LogWarning(ex, "Failed to remove obsolete prompt file: {File}", fileName);
                    }
                }
            }

            // Validate copy
            if (failed > 0)
            {
                _logger.LogWarning("Prompt sync completed with errors. Copied: {Copied}, Failed: {Failed}, Removed: {Removed}",
                    copied, failed, removed);
                return new PromptSyncResult(false, copied, failed, errors);
            }

            _logger.LogInformation("Prompt sync completed successfully. Copied {Copied} files, removed {Removed} obsolete files.",
                copied, removed);
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

        string expandedPath;
        if (path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expandedPath = Path.Combine(home, path[2..]);
        }
        else
        {
            expandedPath = path;
        }

        // Get full path to normalize and prevent path traversal
        var fullPath = Path.GetFullPath(expandedPath);

        // Validate no path traversal outside expected directories
        if (fullPath.Contains(".."))
        {
            throw new ArgumentException($"Invalid path - path traversal detected: {path}");
        }

        return fullPath;
    }
}
