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
    private readonly ILogger<SpeechTranscriberFactoryWarmupService> _logger;

    public SpeechTranscriberFactoryWarmupService(
        ISpeechTranscriberFactory factory,
        ILogger<SpeechTranscriberFactoryWarmupService> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _factory.WarmupAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to warm STT provider ID cache at startup; first transcription will fall back to synchronous load");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
