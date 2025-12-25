using Microsoft.Extensions.Logging;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Orchestration;
using LibraryChain = Olbrasoft.TextToSpeech.Orchestration.ITtsProviderChain;
using LibraryStatus = Olbrasoft.TextToSpeech.Orchestration.ProviderChainStatus;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Adapter that bridges the VirtualAssistant's ITtsProviderChain interface
/// with the new TextToSpeech library's ITtsProviderChain implementation.
/// </summary>
/// <remarks>
/// This adapter allows gradual migration - TtsService continues working unchanged
/// while we switch to the new library's implementation underneath.
/// </remarks>
public sealed class TtsProviderChainAdapter : ITtsProviderChain
{
    private readonly ILogger<TtsProviderChainAdapter> _logger;
    private readonly LibraryChain _libraryChain;

    public TtsProviderChainAdapter(
        ILogger<TtsProviderChainAdapter> logger,
        LibraryChain libraryChain)
    {
        _logger = logger;
        _libraryChain = libraryChain;

        _logger.LogInformation("TtsProviderChainAdapter initialized - using TextToSpeech library");
    }

    /// <inheritdoc />
    public async Task<(byte[]? Audio, string? ProviderUsed)> SynthesizeAsync(
        string text,
        VoiceConfig config,
        string? sourceProfile = null,
        CancellationToken cancellationToken = default)
    {
        // Convert VoiceConfig to TtsRequest (legacy method)
        var request = new TtsRequest
        {
            Text = text,
            Voice = config.Voice,
            Rate = ParseRate(config.Rate) ?? 0,
            Pitch = ParsePitch(config.Pitch) ?? 0,
            PreferredProvider = sourceProfile // Legacy sourceProfile for provider selection
        };

        // Call the library
        var result = await _libraryChain.SynthesizeAsync(request, cancellationToken);

        if (!result.Success || result.Audio == null)
        {
            _logger.LogWarning("TTS synthesis failed: {Error}", result.ErrorMessage);
            return (null, result.ProviderUsed);
        }

        // Convert IAudioData to byte[]
        byte[]? audioBytes = result.Audio switch
        {
            MemoryAudioData memory => memory.Data,
            FileAudioData file => await ReadFileAsync(file.FilePath, cancellationToken),
            _ => null
        };

        return (audioBytes, result.ProviderUsed);
    }

    /// <inheritdoc />
    public async Task<(byte[]? Audio, string? ProviderUsed)> SynthesizeAdvancedAsync(
        string text,
        VoiceConfig config,
        TtsContext context,
        CancellationToken cancellationToken = default)
    {
        // Convert VoiceConfig and TtsContext to TtsRequest (advanced method)
        var request = new TtsRequest
        {
            Text = text,
            Voice = context.Voice ?? config.Voice, // Context voice overrides config voice
            Rate = ParseRate(config.Rate) ?? 0,
            Pitch = ParsePitch(config.Pitch) ?? 0,
            AgentName = context.AgentName,
            AgentInstanceId = context.AgentInstanceId,
            ProviderFallbackChain = context.ProviderFallbackChain,
            PreferredProvider = context.SourceProfile // Legacy fallback
        };

        _logger.LogDebug(
            "Advanced TTS request from agent '{Agent}' (instance: {Instance}) with voice '{Voice}' and {ChainLength} providers",
            context.AgentName ?? "Unknown",
            context.AgentInstanceId ?? "N/A",
            request.Voice,
            context.ProviderFallbackChain?.Count ?? 0);

        // Call the library
        var result = await _libraryChain.SynthesizeAsync(request, cancellationToken);

        if (!result.Success || result.Audio == null)
        {
            _logger.LogWarning("TTS synthesis failed: {Error}", result.ErrorMessage);
            return (null, result.ProviderUsed);
        }

        // Convert IAudioData to byte[]
        byte[]? audioBytes = result.Audio switch
        {
            MemoryAudioData memory => memory.Data,
            FileAudioData file => await ReadFileAsync(file.FilePath, cancellationToken),
            _ => null
        };

        return (audioBytes, result.ProviderUsed);
    }

    /// <inheritdoc />
    public IReadOnlyList<TtsProviderStatus> GetProvidersStatus()
    {
        var libraryStatuses = _libraryChain.GetProvidersStatus();

        return libraryStatuses.Select(s => new TtsProviderStatus(
            Name: s.ProviderName,
            IsHealthy: s.CircuitState != Olbrasoft.TextToSpeech.Orchestration.CircuitState.Open,
            LastFailure: null, // Library doesn't track this directly
            NextRetryAt: s.CircuitResetTime,
            ConsecutiveFailures: s.ConsecutiveFailures
        )).ToList();
    }

    /// <inheritdoc />
    public void ResetCircuitBreaker(string? providerName = null)
    {
        // The new library doesn't expose ResetCircuitBreaker directly
        // This is intentional - circuit breakers should reset automatically
        _logger.LogInformation(
            "ResetCircuitBreaker called for '{Provider}' - library manages reset automatically",
            providerName ?? "all");
    }

    /// <summary>
    /// Parses rate string (e.g., "+10%", "-20%", "default") to integer percentage.
    /// </summary>
    private static int? ParseRate(string? rate)
    {
        if (string.IsNullOrEmpty(rate) || rate == "default")
            return null;

        // Remove % and parse
        var normalized = rate.Replace("%", "").Replace("+", "");
        if (int.TryParse(normalized, out var value))
            return value;

        return null;
    }

    /// <summary>
    /// Parses pitch string (e.g., "+5Hz", "-10Hz", "default") to integer.
    /// </summary>
    private static int? ParsePitch(string? pitch)
    {
        if (string.IsNullOrEmpty(pitch) || pitch == "default")
            return null;

        // Remove Hz and parse
        var normalized = pitch.Replace("Hz", "").Replace("+", "");
        if (int.TryParse(normalized, out var value))
            return value;

        return null;
    }

    /// <summary>
    /// Reads file content as byte array.
    /// </summary>
    private static async Task<byte[]?> ReadFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(filePath, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
