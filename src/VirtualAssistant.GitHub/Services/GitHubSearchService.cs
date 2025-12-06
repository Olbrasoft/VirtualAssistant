using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub.Dtos;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Service for semantic search on GitHub issues using pgvector similarity.
/// </summary>
public class GitHubSearchService : IGitHubSearchService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GitHubSearchService> _logger;

    public GitHubSearchService(
        VirtualAssistantDbContext dbContext,
        IEmbeddingService embeddingService,
        ILogger<GitHubSearchService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConfigured => _embeddingService.IsConfigured;

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubIssueDto>> SearchSimilarAsync(
        string query,
        SearchTarget target = SearchTarget.Both,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Empty search query provided");
            return Array.Empty<GitHubIssueDto>();
        }

        if (!_embeddingService.IsConfigured)
        {
            _logger.LogWarning("Embedding service is not configured, cannot perform semantic search");
            return Array.Empty<GitHubIssueDto>();
        }

        // Generate embedding for the search query
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, ct);
        if (queryEmbedding == null)
        {
            _logger.LogWarning("Failed to generate embedding for query: {Query}", query);
            return Array.Empty<GitHubIssueDto>();
        }

        _logger.LogDebug("Searching for similar issues with target: {Target}, limit: {Limit}", target, limit);

        // Perform similarity search based on target
        var results = target switch
        {
            SearchTarget.Title => await SearchByTitleAsync(queryEmbedding, limit, ct),
            SearchTarget.Body => await SearchByBodyAsync(queryEmbedding, limit, ct),
            SearchTarget.Both => await SearchByBothAsync(queryEmbedding, limit, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };

        _logger.LogInformation("Found {Count} similar issues for query: {Query}", results.Count, query);
        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubIssueDto>> GetOpenIssuesAsync(
        string repoFullName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoFullName))
        {
            return Array.Empty<GitHubIssueDto>();
        }

        var issues = await _dbContext.GitHubIssues
            .Include(i => i.Repository)
            .Include(i => i.Agents)
            .Where(i => i.Repository.FullName == repoFullName && i.State == "open")
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync(ct);

        return issues.Select(i => GitHubIssueDto.FromEntity(i)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubIssueDto>> FindDuplicatesAsync(
        string title,
        string? body = null,
        float threshold = 0.8f,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Array.Empty<GitHubIssueDto>();
        }

        if (!_embeddingService.IsConfigured)
        {
            _logger.LogWarning("Embedding service is not configured, cannot find duplicates");
            return Array.Empty<GitHubIssueDto>();
        }

        // Generate embedding for the title
        var titleEmbedding = await _embeddingService.GenerateEmbeddingAsync(title, ct);
        if (titleEmbedding == null)
        {
            _logger.LogWarning("Failed to generate embedding for duplicate check");
            return Array.Empty<GitHubIssueDto>();
        }

        // Search for similar issues in open state only
        var results = await _dbContext.GitHubIssues
            .Include(i => i.Repository)
            .Include(i => i.Agents)
            .Where(i => i.TitleEmbedding != null && i.State == "open")
            .Select(i => new
            {
                Issue = i,
                Similarity = 1 - i.TitleEmbedding!.CosineDistance(titleEmbedding)
            })
            .Where(x => x.Similarity >= threshold)
            .OrderByDescending(x => x.Similarity)
            .Take(10)
            .ToListAsync(ct);

        _logger.LogInformation(
            "Found {Count} potential duplicates for title: {Title} with threshold: {Threshold}",
            results.Count, title, threshold);

        return results.Select(r => GitHubIssueDto.FromEntity(r.Issue, (float)r.Similarity)).ToList();
    }

    private async Task<IReadOnlyList<GitHubIssueDto>> SearchByTitleAsync(
        Vector queryEmbedding,
        int limit,
        CancellationToken ct)
    {
        var results = await _dbContext.GitHubIssues
            .Include(i => i.Repository)
            .Include(i => i.Agents)
            .Where(i => i.TitleEmbedding != null)
            .Select(i => new
            {
                Issue = i,
                Similarity = 1 - i.TitleEmbedding!.CosineDistance(queryEmbedding)
            })
            .OrderByDescending(x => x.Similarity)
            .Take(limit)
            .ToListAsync(ct);

        return results.Select(r => GitHubIssueDto.FromEntity(r.Issue, (float)r.Similarity)).ToList();
    }

    private async Task<IReadOnlyList<GitHubIssueDto>> SearchByBodyAsync(
        Vector queryEmbedding,
        int limit,
        CancellationToken ct)
    {
        var results = await _dbContext.GitHubIssues
            .Include(i => i.Repository)
            .Include(i => i.Agents)
            .Where(i => i.BodyEmbedding != null)
            .Select(i => new
            {
                Issue = i,
                Similarity = 1 - i.BodyEmbedding!.CosineDistance(queryEmbedding)
            })
            .OrderByDescending(x => x.Similarity)
            .Take(limit)
            .ToListAsync(ct);

        return results.Select(r => GitHubIssueDto.FromEntity(r.Issue, (float)r.Similarity)).ToList();
    }

    private async Task<IReadOnlyList<GitHubIssueDto>> SearchByBothAsync(
        Vector queryEmbedding,
        int limit,
        CancellationToken ct)
    {
        // Search by title and body, take the best match for each issue
        var results = await _dbContext.GitHubIssues
            .Include(i => i.Repository)
            .Include(i => i.Agents)
            .Where(i => i.TitleEmbedding != null || i.BodyEmbedding != null)
            .Select(i => new
            {
                Issue = i,
                TitleSimilarity = i.TitleEmbedding != null
                    ? 1 - i.TitleEmbedding.CosineDistance(queryEmbedding)
                    : 0.0,
                BodySimilarity = i.BodyEmbedding != null
                    ? 1 - i.BodyEmbedding.CosineDistance(queryEmbedding)
                    : 0.0
            })
            .Select(x => new
            {
                x.Issue,
                // Take the maximum of title and body similarity
                Similarity = x.TitleSimilarity > x.BodySimilarity ? x.TitleSimilarity : x.BodySimilarity
            })
            .OrderByDescending(x => x.Similarity)
            .Take(limit)
            .ToListAsync(ct);

        return results.Select(r => GitHubIssueDto.FromEntity(r.Issue, (float)r.Similarity)).ToList();
    }
}
