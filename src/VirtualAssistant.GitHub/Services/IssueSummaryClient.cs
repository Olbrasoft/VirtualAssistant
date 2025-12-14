using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// HTTP client for fetching Czech issue summaries from the GitHub.Issues API.
/// </summary>
public class IssueSummaryClient : IIssueSummaryClient
{
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;
    private readonly ILogger<IssueSummaryClient> _logger;

    public IssueSummaryClient(
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        ILogger<IssueSummaryClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IssueSummariesResult> GetSummariesAsync(
        string owner,
        string repo,
        IEnumerable<int> issueNumbers,
        CancellationToken ct = default)
    {
        var issueList = issueNumbers.ToList();
        if (issueList.Count == 0)
        {
            return new IssueSummariesResult();
        }

        if (string.IsNullOrEmpty(_settings.IssuesApiUrl))
        {
            _logger.LogWarning("GitHub.IssuesApiUrl is not configured, cannot fetch summaries");
            return new IssueSummariesResult
            {
                Error = "GitHub.IssuesApiUrl is not configured"
            };
        }

        try
        {
            _logger.LogInformation(
                "Fetching Czech summaries for {Count} issues in {Owner}/{Repo}",
                issueList.Count, owner, repo);

            var request = new
            {
                issueNumbers = issueList,
                owner,
                repo
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_settings.IssuesApiUrl.TrimEnd('/')}/api/issues/summaries",
                request,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "GitHub.Issues API returned {StatusCode}: {Error}",
                    response.StatusCode, errorContent);

                return new IssueSummariesResult
                {
                    Error = $"API returned {response.StatusCode}: {errorContent}"
                };
            }

            var result = await ParseResponseAsync(response, ct);

            _logger.LogInformation(
                "Received {SummaryCount} summaries, {SyncedCount} synced from GitHub, {NotFoundCount} not found",
                result.Summaries.Count, result.SyncedFromGitHub.Count, result.NotFound.Count);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling GitHub.Issues API");
            return new IssueSummariesResult
            {
                Error = $"HTTP error: {ex.Message}"
            };
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request to GitHub.Issues API timed out");
            return new IssueSummariesResult
            {
                Error = "Request timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling GitHub.Issues API");
            return new IssueSummariesResult
            {
                Error = $"Unexpected error: {ex.Message}"
            };
        }
    }

    private async Task<IssueSummariesResult> ParseResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new IssueSummariesResult();

        // Parse summaries
        if (root.TryGetProperty("summaries", out var summariesElement))
        {
            foreach (var prop in summariesElement.EnumerateObject())
            {
                if (int.TryParse(prop.Name, out var issueNumber))
                {
                    var summary = new IssueSummary
                    {
                        IssueNumber = issueNumber,
                        OriginalTitle = GetStringProperty(prop.Value, "originalTitle"),
                        CzechTitle = GetStringProperty(prop.Value, "czechTitle"),
                        CzechSummary = GetStringProperty(prop.Value, "czechSummary"),
                        IsOpen = GetBoolProperty(prop.Value, "isOpen"),
                        Url = GetStringProperty(prop.Value, "url")
                    };
                    result.Summaries[issueNumber] = summary;
                }
            }
        }

        // Parse syncedFromGitHub
        if (root.TryGetProperty("syncedFromGitHub", out var syncedElement))
        {
            result.SyncedFromGitHub = syncedElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Number)
                .Select(e => e.GetInt32())
                .ToList();
        }

        // Parse notFound
        if (root.TryGetProperty("notFound", out var notFoundElement))
        {
            result.NotFound = notFoundElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Number)
                .Select(e => e.GetInt32())
                .ToList();
        }

        return result;
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool GetBoolProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.True;
    }
}
