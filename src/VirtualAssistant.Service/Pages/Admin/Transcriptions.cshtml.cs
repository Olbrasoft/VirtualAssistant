using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmCorrectionQueries;

namespace Olbrasoft.VirtualAssistant.Service.Pages.Admin;

public class TranscriptionsModel : PageModel
{
    private readonly IQueryProcessor _queryProcessor;

    public TranscriptionsModel(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    public class TranscriptionViewModel
    {
        public int Id { get; set; } // LlmCorrection Id
        public required string OriginalText { get; set; }
        public required string CorrectedText { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? PromptName { get; set; }
        public int? ProcessDurationMs { get; set; }
        // Whisper metadata
        public int? AudioDurationMs { get; set; }
        public int WhisperId { get; set; }
    }

    public List<TranscriptionViewModel> Items { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public int TotalPages { get; set; }
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public async Task OnGetAsync()
    {
        // Default to first page
        if (PageIndex < 1) PageIndex = 1;

        var query = new GetLlmCorrectionsWithPromptsQuery(
            SearchString, 
            StartDate, 
            EndDate, 
            PageIndex, 
            20);

        var result = await _queryProcessor.ProcessAsync(query, CancellationToken.None);

        TotalPages = (int)Math.Ceiling(result.TotalCount / (double)20);
        if (TotalPages > 0 && PageIndex > TotalPages) PageIndex = TotalPages;

        Items = result.Select(c => new TranscriptionViewModel
        {
            Id = c.Id,
            OriginalText = c.WhisperTranscription.TranscribedText,
            CorrectedText = c.CorrectedText,
            CreatedAt = c.CreatedAt,
            PromptName = c.Prompt != null ? c.Prompt.Name : "N/A",
            ProcessDurationMs = c.DurationMs,
            AudioDurationMs = c.WhisperTranscription.AudioDurationMs,
            WhisperId = c.WhisperTranscriptionId
        }).ToList();
    }
}
