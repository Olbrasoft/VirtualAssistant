using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.ProviderQueries;

/// <summary>
/// Query to get all providers of a specific type (e.g., "stt", "tts", "llm").
/// Used by ISpeechTranscriberFactory to load provider IDs into cache at startup.
/// </summary>
/// <param name="Type">Provider type (e.g., "stt", "tts", "llm").</param>
public record GetProvidersByTypeQuery(string Type) : IQuery<IReadOnlyList<Provider>>;
