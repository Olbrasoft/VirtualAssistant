using System.Security.Cryptography;
using System.Text;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for managing TTS audio file cache.
/// Caches generated audio files to avoid regenerating identical text.
/// </summary>
public interface ITtsCacheService
{
    /// <summary>
    /// Gets the cache file path for the given text and voice configuration.
    /// </summary>
    /// <param name="text">Text to be spoken</param>
    /// <param name="config">Voice configuration</param>
    /// <returns>Full path to the cache file</returns>
    string GetCachePath(string text, VoiceConfig config);

    /// <summary>
    /// Tries to get a cached audio file for the given text.
    /// </summary>
    /// <param name="text">Text to be spoken</param>
    /// <param name="config">Voice configuration</param>
    /// <param name="path">Path to cached file if found</param>
    /// <returns>True if cached file exists, false otherwise</returns>
    bool TryGetCached(string text, VoiceConfig config, out string path);

    /// <summary>
    /// Saves audio data to cache.
    /// </summary>
    /// <param name="text">Text that was spoken</param>
    /// <param name="config">Voice configuration used</param>
    /// <param name="audioData">Audio data to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(string text, VoiceConfig config, byte[] audioData, CancellationToken cancellationToken = default);
}

/// <summary>
/// File-based TTS cache service implementation.
/// Stores audio files in ~/.cache/virtual-assistant-tts/
/// </summary>
public sealed class TtsCacheService : ITtsCacheService
{
    private readonly string _cacheDirectory;

    public TtsCacheService(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "virtual-assistant-tts");

        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <inheritdoc />
    public string GetCachePath(string text, VoiceConfig config)
    {
        var safeName = new string(text
            .Take(50)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-')
            .ToLowerInvariant();

        var parameters = $"{config.Voice}{config.Rate}{config.Volume}{config.Pitch}";
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(parameters)))[..8];

        return Path.Combine(_cacheDirectory, $"{safeName}-{hash}.mp3");
    }

    /// <inheritdoc />
    public bool TryGetCached(string text, VoiceConfig config, out string path)
    {
        path = GetCachePath(text, config);
        return File.Exists(path);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string text, VoiceConfig config, byte[] audioData, CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(text, config);
        await File.WriteAllBytesAsync(path, audioData, cancellationToken);
    }
}
