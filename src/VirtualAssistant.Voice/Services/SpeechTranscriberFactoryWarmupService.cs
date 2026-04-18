using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Startup hosted service that pre-loads the STT provider ID cache in
/// <see cref="ISpeechTranscriberFactory"/> so the transcription hot path
/// never has to trigger the synchronous DB round-trip inside
/// <c>EnsureProviderIdCacheLoaded</c>.
/// </summary>
public class SpeechTranscriberFactoryWarmupService : IHostedService
{
    private readonly ISpeechTranscriberFactory _factory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SpeechTranscriberFactoryWarmupService> _logger;

    public SpeechTranscriberFactoryWarmupService(
        ISpeechTranscriberFactory factory,
        IHostApplicationLifetime lifetime,
        ILogger<SpeechTranscriberFactoryWarmupService> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Use ApplicationStopping instead of the IHostedService.StartAsync
        // token so the DB query can still be aborted cleanly once the host
        // begins shutting down. The StartAsync token covers only the startup
        // window itself. (Copilot review on PR #1017.)
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetime.ApplicationStopping);

        try
        {
            await _factory.WarmupAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.Token.IsCancellationRequested)
        {
            // Host is shutting down / startup is being canceled — stay silent.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to warm STT provider ID cache at startup; first transcription will fall back to synchronous load");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
