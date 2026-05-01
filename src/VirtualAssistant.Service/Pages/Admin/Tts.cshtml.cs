using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Olbrasoft.VirtualAssistant.Service.Pages.Admin;

/// <summary>
/// Per-key TTS usage dashboard. Renders the static shell; per-key rows are
/// hydrated client-side by polling /api/tts/keys-usage.
/// </summary>
public class TtsModel : PageModel
{
    public void OnGet()
    {
    }
}
