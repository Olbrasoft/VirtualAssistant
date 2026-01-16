using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.LlmModelQueries;

/// <summary>
/// Query to get an LlmModel by its model identifier (e.g., "mistral-large-latest", "alpha-glm-4.7").
/// Returns null if model not found.
/// </summary>
public record GetLlmModelByIdentifierQuery(string ModelIdentifier) : IQuery<LlmModel?>;
