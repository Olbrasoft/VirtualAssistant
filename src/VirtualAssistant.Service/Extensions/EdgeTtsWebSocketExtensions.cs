extern alias EdgeTtsWebSocket;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using EdgeTtsConfig = EdgeTtsWebSocket::Olbrasoft.TextToSpeech.Providers.EdgeTTS.EdgeTtsConfiguration;
using EdgeTtsWsProvider = EdgeTtsWebSocket::Olbrasoft.TextToSpeech.Providers.EdgeTTS.EdgeTtsProvider;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering EdgeTTS WebSocket provider.
/// This temporarily overrides the HTTP-based EdgeTTS provider from TextToSpeech.Providers
/// with a direct WebSocket implementation during development.
/// </summary>
public static class EdgeTtsWebSocketExtensions
{
    /// <summary>
    /// Adds the EdgeTTS WebSocket provider to replace the HTTP-based provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEdgeTtsWebSocketProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure EdgeTTS settings from appsettings.json
        services.Configure<EdgeTtsConfig>(
            configuration.GetSection(EdgeTtsConfig.SectionName));

        // Register as ITtsProvider (will be picked up by orchestration)
        services.AddSingleton<ITtsProvider, EdgeTtsWsProvider>();

        return services;
    }
}
