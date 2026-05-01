using Olbrasoft.TextToSpeech.Providers.GoogleCloud;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Default <see cref="IKeysUsageReporter"/> implementation that delegates to
/// the registered <see cref="GoogleCloudMultiKeyTtsProvider"/> singleton.
/// </summary>
public sealed class GoogleCloudKeysUsageReporter : IKeysUsageReporter
{
    private readonly GoogleCloudMultiKeyTtsProvider _provider;
    public GoogleCloudKeysUsageReporter(GoogleCloudMultiKeyTtsProvider provider) => _provider = provider;
    public IReadOnlyList<ApiKeyUsageSnapshot> GetKeysUsage() => _provider.GetKeysUsage();
}
