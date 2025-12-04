using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Exceptions;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Base class for OpenAI-compatible LLM router services.
/// Contains shared logic for system prompt, parsing, and context management.
/// </summary>
public abstract class BaseLlmRouterService : ILlmRouterService
{
    protected readonly ILogger _logger;
    protected readonly HttpClient _httpClient;
    protected readonly string _model;

    // Recent context for multi-turn awareness
    private readonly Queue<ContextEntry> _recentContext = new();
    private const int MaxContextEntries = 5;

    public abstract string ProviderName { get; }
    
    /// <summary>
    /// The LLM provider enum value for this service
    /// </summary>
    public abstract LlmProvider Provider { get; }

    protected BaseLlmRouterService(ILogger logger, HttpClient httpClient, string model)
    {
        _logger = logger;
        _httpClient = httpClient;
        _model = model;
    }

    public virtual async Task<LlmRouterResult> RouteAsync(string inputText, bool isDiscussionActive = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return LlmRouterResult.Ignored("Empty input");
        }

        var stopwatch = Stopwatch.StartNew();

        var systemPrompt = BuildSystemPrompt(isDiscussionActive);
        var userMessage = $"Voice assistant zachytil: \"{inputText}\"";

        var request = new LlmRequest
        {
            Model = _model,
            Messages =
            [
                new LlmMessage { Role = "system", Content = systemPrompt },
                new LlmMessage { Role = "user", Content = userMessage }
            ],
            Temperature = 0.2f,
            MaxTokens = 256
        };

        // No retry for rate limiting - throw exception for MultiProviderRouter to handle
        try
        {
            _logger.LogDebug("Sending to {Provider}: {Input}", ProviderName, inputText);

            // Use explicit StringContent to ensure Content-Length header is set (required by Cerebras)
            var requestJson = JsonSerializer.Serialize(request);
            using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("chat/completions", requestContent, cancellationToken);

            // Handle rate limiting - throw exception instead of retry
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("{Provider} rate limited (429): {Body}", ProviderName, errorBody);
                
                // Try to parse reset time from error message
                var resetAt = ParseResetTimeFromError(errorBody);
                
                throw new RateLimitException(Provider, $"{ProviderName} rate limited", resetAt ?? DateTime.UtcNow.AddMinutes(5));
            }

            response.EnsureSuccessStatusCode();

            var llmResponse = await response.Content.ReadFromJsonAsync<LlmResponse>(cancellationToken: cancellationToken);
            stopwatch.Stop();

            var content = llmResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Empty response from {Provider}", ProviderName);
                return LlmRouterResult.Error("Empty response", (int)stopwatch.ElapsedMilliseconds);
            }

            // Parse JSON response
            var result = ParseLlmResponse(content, (int)stopwatch.ElapsedMilliseconds);

            // Add to context
            AddToContext(inputText, result);

            _logger.LogInformation(
                "{Provider} routing: {Action} (confidence: {Confidence:F2}, time: {Time}ms)",
                ProviderName, result.Action, result.Confidence, result.ResponseTimeMs);

            return result;
        }
        catch (RateLimitException)
        {
            // Re-throw rate limit exceptions for MultiProviderRouter to handle
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP error calling {Provider} API", ProviderName);
            return LlmRouterResult.Error($"HTTP error: {ex.Message}", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "{Provider} API call timed out", ProviderName);
            return LlmRouterResult.Error("Timeout", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error calling {Provider} API", ProviderName);
            return LlmRouterResult.Error(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Parse reset time from rate limit error message (e.g., "Please try again in 11m51.936s")
    /// </summary>
    private static DateTime? ParseResetTimeFromError(string errorBody)
    {
        try
        {
            // Pattern: "Please try again in Xm Y.Zs" or "Please try again in Y.Zs"
            var match = Regex.Match(errorBody, @"try again in (\d+)m([\d.]+)s");
            if (match.Success)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddMinutes(minutes).AddSeconds(seconds);
            }
            
            // Pattern: just seconds
            match = Regex.Match(errorBody, @"try again in ([\d.]+)s");
            if (match.Success)
            {
                var seconds = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddSeconds(seconds);
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        
        return null;
    }

    private string BuildSystemPrompt(bool isDiscussionActive)
    {
        var now = DateTime.Now;
        var dayOfWeek = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "pondělí",
            DayOfWeek.Tuesday => "úterý",
            DayOfWeek.Wednesday => "středa",
            DayOfWeek.Thursday => "čtvrtek",
            DayOfWeek.Friday => "pátek",
            DayOfWeek.Saturday => "sobota",
            DayOfWeek.Sunday => "neděle",
            _ => now.DayOfWeek.ToString()
        };

        var contextSection = "";
        if (_recentContext.Count > 0)
        {
            var contextLines = _recentContext
                .Select(c => $"- [{c.Timestamp:HH:mm:ss}] \"{c.Input}\" → {c.Action}")
                .ToList();
            contextSection = $@"

PŘEDCHOZÍ KONTEXT (posledních {_recentContext.Count} interakcí):
{string.Join("\n", contextLines)}";
        }

        // Warning for active discussion mode
        var discussionWarning = isDiscussionActive ? @"

⚠️ DŮLEŽITÉ - DISKUZNÍ MÓD JE AKTIVNÍ!
Uživatel již zahájil diskuzi/plánování. NEPOUŽÍVEJ action ""start_discussion""!
Všechny prompty posílej jako ""opencode"" s prompt_type ""Question"".
Pouze pokud uživatel explicitně říká ""konec diskuze"" nebo ""ukončit diskuzi"", použij ""end_discussion"".
" : "";

        return $@"Jsi Voice Router - součást voice assistenta běžícího na Linuxovém desktopu.

KONTEXT:
- Uživatel má spuštěný program OpenCode (AI coding agent v terminálu)
- Voice assistant průběžně zachytává hlasový vstup
- Wake word je ""počítači"" - to je standardní oslovení počítače/asistenta
- Alternativní wake words: ""open code"", ""open kód"", ""openkód""
- Aktuální čas: {now:HH:mm}
- Aktuální datum: {now:d.M.yyyy} ({dayOfWeek}){contextSection}{discussionWarning}

TVŮJ ÚKOL:
Analyzuj zachycený text a rozhodni, jak s ním naložit:

⚠️ DŮLEŽITÉ POŘADÍ VYHODNOCENÍ:

0. **DISKUZE** - KONTROLUJ ÚPLNĚ PRVNÍ! MÁ ABSOLUTNÍ PRIORITU!

   ⚠️ KRITICKÉ: Reaguj POUZE na klíčová slova ""diskutovat"" nebo ""diskuze""!
   Ostatní fráze (probrat, prodiskutovat, plánovat, položit otázku, povídat) NEJSOU diskuze!

   a) **ZAHÁJENÍ DISKUZE** (action: ""start_discussion"")
      - Uživatel chce zahájit diskuzi - POUZE pokud text obsahuje:
        - ""diskutovat"" (pojďme diskutovat, chci diskutovat, budeme diskutovat)
        - ""diskuze"" (nová diskuze, bude diskuze, zahajuji diskuzi)
      - ⚠️ NEPOUŽÍVEJ pro: ""probrat"", ""prodiskutovat"", ""plánovat"", ""povídat"", ""položit otázku""
      - Vrať ""discussion_topic"" s tématem diskuze

   b) **UKONČENÍ DISKUZE** (action: ""end_discussion"")
      - Uživatel chce ukončit probíhající diskuzi/plánování
      - Klíčové fráze: ""konec diskuze"", ""diskuze je ukončená"", ""hotovo s plánováním"", 
        ""plánování ukončeno"", ""to je vše"", ""ukončit diskuzi"", ""ukončujeme diskuzi"",
        ""končíme diskuzi"", ""ukončuji diskuzi"", ""končíme s plánováním""
      - ⚠️ KRITICKÉ: Pokud text obsahuje ukončení diskuze A zároveň další příkaz
        (např. ""ukončujeme diskuzi a naimplementuj to""), VŽDY použij end_discussion!
        Ukončení diskuze má ABSOLUTNÍ PRIORITU. Další příkaz zpracuješ v dalším promptu.

1. **SAVE NOTE** (action: ""savenote"") - UKLÁDÁNÍ POZNÁMEK - KONTROLUJ PRVNÍ!
   - POKUD text začíná nebo obsahuje: ""zapiš si"", ""zapiš poznámku"", ""poznámka"", ""napadlo mě"", ""nezapomeň"", ""připomeň mi"" → VŽDY použij savenote!
   - Vrať ""note_title"" (krátký název souboru, bez diakritiky, kebab-case) a ""note_content"" (obsah poznámky)
   - DŮLEŽITÉ: note_title MUSÍ být bez diakritiky, malými písmeny, slova spojená pomlčkou

2. **ROUTE to OpenCode** (action: ""opencode"") - PROGRAMOVÁNÍ A PŘÍKAZY
   - Cokoliv co obsahuje wake word (""počítači"", ""open code"", ""openkód"") - do OpenCode!
   - ""Počítači"" je regulérní oslovení = routuj do OpenCode!
   - Příkazy pro programování, práci s kódem, soubory, terminálem
   - Technické dotazy vyžadující kontext projektu
   - Příkazy jako: ""vytvoř"", ""oprav"", ""najdi"", ""spusť testy"", ""commitni""
   - Jakékoliv komplexní požadavky nebo dotazy
   - Když si nejsi jistý - pošli do OpenCode!
   - Otevírání aplikací: ""otevři VS Code"", ""spusť prohlížeč"" - TAKÉ do OpenCode!
   - Spouštění příkazů, bash, terminál - VŽDY do OpenCode!

3. **RESPOND directly** (action: ""respond"") 
   - POUZE jednoduché faktické dotazy bez potřeby kontextu
   - Čas, datum, den v týdnu
   - Jednoduché výpočty (2+2)
   - Vrať odpověď v ""response"" poli - KRÁTCE, pro TTS přehrání (1-2 věty)

4. **IGNORE** (action: ""ignore"")
   - Náhodná konverzace s někým jiným (bez wake word)
   - Neúplné věty, šum
   - Text bez jasného záměru a bez wake word

ODPOVĚZ POUZE TÍMTO JSON (žádný další text):
{{
    ""action"": ""opencode"" | ""savenote"" | ""respond"" | ""start_discussion"" | ""end_discussion"" | ""ignore"",
    ""prompt_type"": ""Command"" | ""Question"" | ""Acknowledgement"" | ""Confirmation"" | ""Continuation"",
    ""confidence"": 0.0-1.0,
    ""reason"": ""krátké zdůvodnění"",
    ""response"": ""odpověď pro TTS (pokud action=respond, jinak null)"",
    ""command_for_opencode"": ""shrnutí příkazu (pouze pokud action=opencode, jinak null)"",
    ""note_title"": ""nazev-poznamky-bez-diakritiky (pouze pokud action=savenote)"",
    ""note_content"": ""Obsah poznámky (pouze pokud action=savenote)"",
    ""discussion_topic"": ""téma diskuze (pouze pokud action=start_discussion)""
}}

POLE prompt_type (určuje režim zpracování v OpenCode):
- ""Command"" = jasný příkaz/instrukce v imperativu → BUILD MODE
   - Příkazy: ""vytvoř"", ""oprav"", ""spusť"", ""commitni"", ""otevři"", ""smaž"", ""přidej""
   - Musí být jasný imperativ - co má OpenCode UDĚLAT
- ""Question"" = otázka, dotaz → PLAN MODE (read-only)
   - Otázky: ""jak"", ""co"", ""proč"", ""kde"", ""který"", ""jaký""
   - Dotazy na informace, vysvětlení, analýzu
- ""Acknowledgement"" = oznámení, konstatování → PLAN MODE
   - ""Už je hotovo"", ""dokončil jsem"", ""mám problém"", ""nefunguje mi""
   - Uživatel něco sděluje, ale nežádá akci
- ""Confirmation"" = potvrzení předchozí akce → BUILD MODE
   - ""Ano"", ""Dobře"", ""Správně"", ""Udělej to"", ""Potvrdit""
   - Uživatel potvrzuje navržený postup
- ""Continuation"" = pokračování předchozího úkolu → BUILD MODE
   - ""Pokračuj"", ""Dál"", ""A co dál?"", ""Continue""
   - Navazuje na předchozí kontext

DŮLEŽITÉ: Když si nejsi jistý, použij ""Question"" (bezpečnější volba)";
    }

    private LlmRouterResult ParseLlmResponse(string content, int responseTimeMs)
    {
        try
        {
            // Clean up content - remove markdown code blocks if present
            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join("\n", lines.Skip(1).TakeWhile(l => !l.StartsWith("```")));
            }

            var parsed = JsonSerializer.Deserialize<LlmRouterResponseDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null)
            {
                return LlmRouterResult.Error("Failed to parse JSON", responseTimeMs);
            }

            var action = parsed.Action?.ToLowerInvariant() switch
            {
                "opencode" => LlmRouterAction.OpenCode,
                "respond" => LlmRouterAction.Respond,
                "ignore" => LlmRouterAction.Ignore,
                "savenote" => LlmRouterAction.SaveNote,
                "start_discussion" => LlmRouterAction.StartDiscussion,
                "end_discussion" => LlmRouterAction.EndDiscussion,
                // Bash actions are now redirected to OpenCode
                "bash" => LlmRouterAction.OpenCode,
                _ => LlmRouterAction.Ignore
            };

            // Parse prompt type from string (case-insensitive)
            var promptType = parsed.PromptType?.ToLowerInvariant() switch
            {
                "command" => PromptType.Command,
                "question" => PromptType.Question,
                "acknowledgement" => PromptType.Acknowledgement,
                "confirmation" => PromptType.Confirmation,
                "continuation" => PromptType.Continuation,
                _ => PromptType.Question // Default to Question (safer)
            };

            return new LlmRouterResult
            {
                Action = action,
                PromptType = promptType,
                Confidence = parsed.Confidence,
                Reason = parsed.Reason,
                Response = parsed.Response,
                CommandForOpenCode = parsed.CommandForOpenCode,
                BashCommand = parsed.BashCommand,
                NoteTitle = parsed.NoteTitle,
                NoteContent = parsed.NoteContent,
                DiscussionTopic = parsed.DiscussionTopic,
                ResponseTimeMs = responseTimeMs,
                Success = true
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse {Provider} response: {Content}", ProviderName, content);
            return LlmRouterResult.Error($"JSON parse error: {ex.Message}", responseTimeMs);
        }
    }

    private void AddToContext(string input, LlmRouterResult result)
    {
        _recentContext.Enqueue(new ContextEntry
        {
            Input = input.Length > 100 ? input[..100] + "..." : input,
            Action = result.Action.ToString(),
            Timestamp = DateTime.Now
        });

        while (_recentContext.Count > MaxContextEntries)
        {
            _recentContext.Dequeue();
        }
    }

    private record ContextEntry
    {
        public required string Input { get; init; }
        public required string Action { get; init; }
        public DateTime Timestamp { get; init; }
    }

    #region DTOs

    private class LlmRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required LlmMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private class LlmMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class LlmResponse
    {
        [JsonPropertyName("choices")]
        public LlmChoice[]? Choices { get; set; }
    }

    private class LlmChoice
    {
        [JsonPropertyName("message")]
        public LlmMessage? Message { get; set; }
    }

    private class LlmRouterResponseDto
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("prompt_type")]
        public string? PromptType { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("command_for_opencode")]
        public string? CommandForOpenCode { get; set; }

        [JsonPropertyName("bash_command")]
        public string? BashCommand { get; set; }

        [JsonPropertyName("note_title")]
        public string? NoteTitle { get; set; }

        [JsonPropertyName("note_content")]
        public string? NoteContent { get; set; }

        [JsonPropertyName("discussion_topic")]
        public string? DiscussionTopic { get; set; }
    }

    #endregion
}
