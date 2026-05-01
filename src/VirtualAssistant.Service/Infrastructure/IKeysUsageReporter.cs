using Olbrasoft.TextToSpeech.Providers.GoogleCloud;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Thin abstraction over <see cref="GoogleCloudMultiKeyTtsProvider.GetKeysUsage"/>
/// so controllers / pages can be unit-tested without instantiating the concrete
/// (sealed) provider type.
/// </summary>
public interface IKeysUsageReporter
{
    IReadOnlyList<ApiKeyUsageSnapshot> GetKeysUsage();
}

/// <summary>
/// Default implementation that delegates to the registered
/// <see cref="GoogleCloudMultiKeyTtsProvider"/> singleton.
/// </summary>
public sealed class GoogleCloudKeysUsageReporter : IKeysUsageReporter
{
    private readonly GoogleCloudMultiKeyTtsProvider _provider;
    public GoogleCloudKeysUsageReporter(GoogleCloudMultiKeyTtsProvider provider) => _provider = provider;
    public IReadOnlyList<ApiKeyUsageSnapshot> GetKeysUsage() => _provider.GetKeysUsage();
}
