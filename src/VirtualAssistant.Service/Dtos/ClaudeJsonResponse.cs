using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Service.Dtos;

/// <summary>
/// JSON response from Claude Code headless mode.
/// </summary>
public class ClaudeJsonResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("total_cost_usd")]
    public decimal? TotalCostUsd { get; set; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}
