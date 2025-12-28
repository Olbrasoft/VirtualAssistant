using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Service.Dtos;

/// <summary>
/// JSON response from Claude Code headless mode.
/// </summary>
public class ClaudeJsonResponse
{
    /// <summary>
    /// Gets or sets the type of the response message.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the subtype of the response message.
    /// </summary>
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    /// <summary>
    /// Gets or sets the total cost in USD for this Claude API request.
    /// </summary>
    [JsonPropertyName("total_cost_usd")]
    public decimal? TotalCostUsd { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this response represents an error.
    /// </summary>
    [JsonPropertyName("is_error")]
    public bool? IsError { get; set; }

    /// <summary>
    /// Gets or sets the result content from Claude's response.
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    /// <summary>
    /// Gets or sets the unique session identifier for this Claude conversation.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}
