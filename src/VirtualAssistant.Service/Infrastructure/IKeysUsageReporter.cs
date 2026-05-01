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
