using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore;

namespace Olbrasoft.VirtualAssistant.Service.Pages.Admin;

public class TranscriptionsModel : PageModel
{
    private readonly VirtualAssistantDbContext _context;

    public TranscriptionsModel(VirtualAssistantDbContext context)
    {
        _context = context;
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
        IQueryable<LlmCorrection> query = _context.LlmCorrections
            .Include(c => c.WhisperTranscription)
            .Include(c => c.Prompt);

        if (!string.IsNullOrEmpty(SearchString))
        {
            query = query.Where(c => 
                c.CorrectedText.Contains(SearchString) || 
                c.WhisperTranscription.TranscribedText.Contains(SearchString));
        }

        if (StartDate.HasValue)
        {
            // Input is likely local date (00:00), convert to UTC
            var utcStart = StartDate.Value.ToUniversalTime();
            query = query.Where(c => c.CreatedAt >= utcStart);
        }

        if (EndDate.HasValue)
        {
            // End of the day in UTC effectively
            var utcEnd = EndDate.Value.ToUniversalTime().AddDays(1);
            query = query.Where(c => c.CreatedAt < utcEnd);
        }

        var totalItems = await query.CountAsync();
        int pageSize = 20;
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Ensure PageIndex is valid
        if (PageIndex < 1) PageIndex = 1;
        if (TotalPages > 0 && PageIndex > TotalPages) PageIndex = TotalPages;

        Items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((PageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new TranscriptionViewModel
            {
                Id = c.Id,
                OriginalText = c.WhisperTranscription.TranscribedText,
                CorrectedText = c.CorrectedText,
                CreatedAt = c.CreatedAt,
                PromptName = c.Prompt != null ? c.Prompt.Name : "N/A",
                ProcessDurationMs = c.DurationMs,
                AudioDurationMs = c.WhisperTranscription.AudioDurationMs,
                WhisperId = c.WhisperTranscriptionId
            })
            .ToListAsync();
    }
}
