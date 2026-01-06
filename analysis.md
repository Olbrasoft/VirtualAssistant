# VirtualAssistant - Kompletní Analýza Projektu

**Datum analýzy:** 6. ledna 2026  
**Verze:** .NET 10.0  
**Databáze:** PostgreSQL 16+ s pgvector (768d)

---

## Executive Summary

VirtualAssistant je dobře navržený Linux voice-controlled assistant s event-driven architekturou. Projekt má **silný základ** s Clean Architecture + CQRS patterns, ale obsahuje několik architektonických problémů vyžadujících pozornost.

### Klíčová zjištění:

| Oblast | Hodnocení | Komentář |
|--------|-----------|----------|
| **Architektura** | ✅ Dobrá | Clean Architecture, CQRS, event-driven |
| **Kód** | ⚠️ Střední | Některé code smells, dead code |
| **Testy** | ⚠️ Nerovnoměrné | 407 testů, ale kritické mezery |
| **SOLID** | ⚠️ Částečné porušení | SRP violations v několika službách |
| **Bezpečnost** | ⚠️ Vyžaduje pozornost | Volitelná webhook validace |

### Kritické problémy (vyžadují okamžitou pozornost):
1. **Chybějící unit testy controllerů** - Controllery nejsou pokryty testy
2. **Fire-and-forget tasks** bez cancellation support
3. **Unbounded VAD buffer** - potenciální memory leak
4. **Audio truncation** ztrácí začátek řeči

### Střední problémy:
5. **Volitelná webhook secret validace** - bezpečnostní riziko (pouze pokud je port veřejně přístupný)

---

## 1. Architektura

### 1.1 Struktura projektu

```
VirtualAssistant/
├── src/
│   ├── VirtualAssistant.Service      # ASP.NET Core host (port 5055)
│   ├── VirtualAssistant.Core         # Domain logic, events, state machine
│   ├── VirtualAssistant.Voice        # TTS/STT, VAD, audio processing
│   ├── VirtualAssistant.Data         # Entities, DTOs, CQRS commands/queries
│   ├── VirtualAssistant.Data.EntityFrameworkCore  # DbContext, migrations
│   ├── VirtualAssistant.GitHub       # GitHub API integrace
│   ├── VirtualAssistant.Desktop      # GNOME desktop integration
│   ├── VirtualAssistant.LlmChain     # Multi-provider LLM routing
│   └── VirtualAssistant.Api          # Minimal API endpoints
└── tests/
    └── [odpovídající test projekty]
```

### 1.2 Pozitiva

✅ **Event-Driven Voice Pipeline**
- 4 workers: AudioCapturer → VoiceActivity → TranscriptionRouter → ActionExecutor
- InMemoryEventBus s thread-safe pub/sub (Observer pattern)

✅ **Circuit Breaker Pattern pro TTS**
- Provider chain: AzureTTS → EdgeTTS → VoiceRss → Google → Piper
- Automatický fallback při selhání

✅ **CQRS Pattern v Data vrstvě**
- Oddělené commands a queries
- Generic handlery s SQL injection protection

✅ **PostgreSQL + pgvector**
- 768d vektory pro sémantické vyhledávání GitHub issues
- Ollama embeddings (nomic-embed-text)

✅ **Dependency Injection**
- Extension methods pro modulární registraci
- Interface segregation

### 1.3 Problémy a doporučení

#### 🔴 KRITICKÉ: Clean Architecture Violation

**Soubor:** `VirtualAssistant.Core.csproj`

```xml
<!-- PROBLÉM: Core vrstva závisí na Infrastructure -->
<ProjectReference Include="..\VirtualAssistant.Data.EntityFrameworkCore\..." />
```

**Dopad:** Core layer by neměl záviset na EF Core - porušuje dependency inversion.

**Doporučení:** Přesunout `NotificationService` do Service vrstvy nebo vytvořit repository abstrakci.

---

#### 🔴 KRITICKÉ: Fire-and-Forget Tasks

**Soubory:**
- `GitHubWebhooksController.cs:173`
- `EndpointExtensions.cs:105, 159`

```csharp
// PROBLÉM: Task běží bez sledování
_ = Task.Run(async () => { /* deployment */ });
```

**Dopad:** 
- Žádná podpora pro graceful shutdown
- Selhání se nezalogují
- Nelze zrušit při ukončení aplikace

**Doporučení:**
```csharp
// Implementovat IBackgroundTaskManager
var taskManager = app.Services.GetRequiredService<IBackgroundTaskManager>();
taskManager.QueueTask(cancellationToken => DeployAsync(cancellationToken));
```

---

#### 🟡 STŘEDNÍ: Anemic Service Layer v Core

**Soubor:** `VirtualAssistant.Core/Services/`

**Problém:** 23 z 25 interface má pouze definici bez implementace v Core.

**Dopad:** Implementace roztroušeny v jiných projektech, ztížená discoverability.

**Doporučení:** Buď implementovat v Core, nebo přesunout interfaces do VirtualAssistant.Data.

---

## 2. Naming Conventions

### 2.1 Pozitiva

✅ Konzistentní `I` prefix pro interfaces  
✅ Clear naming: `NotificationService`, `VoiceStateMachine`, `TtsProviderChain`  
✅ PostgreSQL snake_case v DB (`is_active`, `created_at`)  
✅ Namespace organizace: `VirtualAssistant.{Layer}.{Category}`

### 2.2 Problémy

| Problém | Soubor | Doporučení |
|---------|--------|------------|
| Route mismatch | `ClipboardController.cs:12` - route `/api/hub` ale účel je clipboard | Přejmenovat na `/api/clipboard` |
| Ambiguous name | `IStateNotificationHandler` | Přejmenovat na `IVoiceStateNotificationHandler` |
| Inconsistent | `IVirtualAssistantSpeaker` vs `ITtsProviderChain` | Sjednotit naming pattern |

---

## 3. SOLID Principles

### 3.1 Single Responsibility Violations

#### NotificationService (231 lines)
**Soubor:** `VirtualAssistant.Core/Services/NotificationService.cs`

**Problém:** Zpracovává 4 různé odpovědnosti:
- Agent lookup (lines 26-45)
- Notification persistence (lines 46-85)
- TTS tracking (lines 156-229)
- Provider upsert (lines 177-201)

**Doporučení:** Rozdělit na:
- `NotificationPersistenceService`
- `TtsOutcomeTracker`
- `ProviderRepository`

---

#### DictationWorker (384 lines)
**Soubor:** `VirtualAssistant.Service/Workers/DictationWorker.cs`

**Problém:** Kombinuje:
- Keyboard monitoring
- State transitions
- Audio recording
- Transcription
- Keyboard simulation

**Doporučení:** Rozdělit na:
- `KeyboardEventHandler`
- `DictationOrchestrator`
- `AudioRecordingManager`

---

### 3.2 Open/Closed Violations

**Soubor:** `RateLimitParser.cs:44-48`

```csharp
// PROBLÉM: Hardcoded regex patterns
private static readonly Regex[] Patterns = { ... };
```

**Doporučení:** Načítat patterns z konfigurace nebo použít strategy pattern.

---

### 3.3 Dependency Inversion Violations

**Soubor:** `SettingsService.cs:21`

```csharp
// PROBLÉM: Přímá závislost na OS API
var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
```

**Doporučení:** Inject `IFileSystemAbstraction` nebo `IConfigurationPathProvider`.

---

## 4. Code Smells a Technický Dluh

### 4.1 Kritické

| Problém | Soubor:Řádek | Severity | Popis |
|---------|--------------|----------|-------|
| Unbounded buffer | `VadService.cs:26` | 🔴 HIGH | `_sampleBuffer` může růst neomezeně |
| Audio truncation | `TranscriptionService.cs:142` | 🔴 HIGH | Bere poslední bytes, ztrácí začátek řeči |
| Race condition | `NotificationService.cs:177-201` | 🟡 MEDIUM | Provider lookup + create není atomické |
| Dead code | `TtsController.cs:45-156` | 🟡 MEDIUM | `TryEdgeTtsAsync()`, `SpeakWithPiper()` nikdy nevolány |

### 4.2 Střední

| Problém | Soubor:Řádek | Severity | Popis |
|---------|--------------|----------|-------|
| Bare catch block | `RateLimitParser.cs:36` | 🟡 MEDIUM | `catch { }` skrývá chyby |
| Lock in async | `AudioRecordingCoordinator.cs:47-94` | 🟡 MEDIUM | `lock()` v async metodě |
| Static factory v interface | `IClaudeDispatchService.cs:35-104` | 🟡 MEDIUM | Factory methods patří do builder class |
| Triple lock check | `TtsService.cs:69,150,195` | 🟡 MEDIUM | Redundantní, stačí 1 check |
| Hardcoded VAD threshold | `VadService.cs:20` | 🟡 MEDIUM | `0.5f` by mělo být konfigurovatelné |

### 4.3 Nízké

| Problém | Soubor | Severity | Popis |
|---------|--------|----------|-------|
| Missing unique index | `ProviderConfiguration.cs` | 🟢 LOW | Provider.Name může být duplicitní |
| DateTime inconsistency | Multiple entities | 🟢 LOW | Mix `DateTime` a `DateTimeOffset` |
| Duplicate DTOs | `DispatchTaskResult` vs `DispatchTaskResponse` | 🟢 LOW | Podobná struktura, nejasné použití |

---

## 5. Bezpečnost

### 5.1 Střední riziko (pouze při veřejném přístupu)

#### Volitelná Webhook Validace
**Soubor:** `GitHubWebhooksController.cs:52-61`

```csharp
// Validace se provede pouze pokud jsou OBĚ hodnoty nastavené
if (!string.IsNullOrEmpty(webhookSecret) && !string.IsNullOrEmpty(signature))
{
    if (!VerifySignature(body, signature, webhookSecret))
        return Unauthorized("Invalid signature");
}
```

**Aktuální stav:** Port 5055 je pouze localhost - není kritické.

**Pokud by se port vystavil veřejně**, doporučení:
```csharp
var webhookSecret = _configuration["GitHub:WebhookSecret"] 
    ?? throw new InvalidOperationException("GitHub webhook secret not configured");
```

---

#### SignalR Hub bez autentizace
**Soubor:** `EndpointExtensions.cs:202-211`

**Aktuální stav:** Pouze localhost - není kritické.

**Pokud by se port vystavil veřejně:** Desktop context data (workspace, window titles) by byla broadcastována bez autentizace.

---

### 5.2 Stávající dobré praktiky

✅ Secrets v systemd env, ne v appsettings.json
✅ Port 5055 pouze localhost
✅ HMAC-SHA256 verifikace implementována správně (když je secret nastaven)

---

## 6. Testování

### 6.1 Přehled

| Metrika | Hodnota |
|---------|---------|
| **Celkem testů** | ~407 |
| **Test projektů** | 8 |
| **Framework** | xUnit + Moq ✅ |
| **Pokrytí** | Nerovnoměrné |

### 6.2 Pravidla pro testy

> **DŮLEŽITÉ:** Všechny testy v tomto projektu jsou **UNIT TESTY** s mockovanými závislostmi.
> 
> - ✅ **ANO:** Mockovat všechny external dependencies (Moq)
> - ✅ **ANO:** Testovat business logiku izolovaně
> - ✅ **ANO:** In-memory database pro EF Core testy
> - ❌ **NE:** Volat skutečná externí API (GitHub, LLM providers, TTS)
> - ❌ **NE:** Integrační testy vyžadující tokeny nebo síťové spojení
> - ❌ **NE:** Testy, které by spotřebovávaly API kredity

**Příklad správného unit testu controlleru:**
```csharp
public class NotificationsControllerTests
{
    private readonly Mock<INotificationService> _serviceMock;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _serviceMock = new Mock<INotificationService>();
        _controller = new NotificationsController(_serviceMock.Object);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        // Arrange - mockovaná odpověď, žádné skutečné volání
        _serviceMock
            .Setup(x => x.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Notification { Id = 1 });

        // Act
        var result = await _controller.Create(new CreateNotificationRequest { Text = "Test" });

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}
```

### 6.3 Coverage Distribution

```
VirtualAssistant.Service.Tests   171 testů (42%)  ✅ EXCELLENT
VirtualAssistant.Voice.Tests     111 testů (27%)  ✅ EXCELLENT
VirtualAssistant.Desktop.Tests    40 testů (10%)  ✅ GOOD
VirtualAssistant.Data.EF.Tests    49 testů (12%)  ✅ GOOD
VirtualAssistant.GitHub.Tests     22 testů  (5%)  ⚠️ MINIMAL
VirtualAssistant.Core.Tests       14 testů  (3%)  ⚠️ SPARSE
VirtualAssistant.Api.Tests         0 testů  (0%)  ❌ EMPTY
VirtualAssistant.LlmChain.Tests    0 testů  (0%)  ❌ PLACEHOLDER
```

### 6.4 Chybějící testy

1. **VirtualAssistant.Api.Tests** - PRÁZDNÝ
   - Chybí **unit testy controllerů** s mockovanými závislostmi
   - **NENÍ** potřeba integrační testy - ty by spotřebovávaly tokeny
   - **Priorita:** VYSOKÁ

2. **VirtualAssistant.LlmChain** - PLACEHOLDER
   - Chybí unit testy pro provider logic (parsing responses, error handling)
   - **NEDĚLAT** integrační testy proti Groq/Cerebras/Mistral API (tokeny, rate limits)
   - Testovat pouze: response parsing, fallback logic, circuit breaker behavior (vše mockované)

3. **GitHubIssueStatusServiceTests** - TRIVIÁLNÍ
   - Testuje pouze DTO properties
   - Chybí behavior testy s mockovaným `IGitHubClient`

### 6.5 Kvalitní příklady (reference)

**Exemplární testy:**
- `DictationSpeechCoordinatorTests.cs` (351 lines, 17 tests) - state machine testing
- `AssistantSpeechTrackerServiceTests.cs` (379 lines, 20 tests) - comprehensive coverage

### 6.6 Timing Issues

**Soubor:** `AudioRecordingCoordinatorTests.cs:276`

```csharp
// PROBLÉM: Timing-dependent test
await Task.Delay(50);  // Může být flaky na pomalých strojích
```

**Doporučení:** Použít `TaskCompletionSource` nebo `ManualResetEvent`.

---

## 7. Akční Položky

### 7.1 Kritické (P0) - Do 1 týdne

| # | Akce | Soubor | Effort |
|---|------|--------|--------|
| 1 | Přidat unit testy controllerů (mockované, bez HTTP) | `tests/VirtualAssistant.Api.Tests/` | 3h |
| 2 | Opravit unbounded VAD buffer | `VadService.cs:26` | 30m |
| 3 | Opravit audio truncation (brát začátek, ne konec) | `TranscriptionService.cs:142` | 30m |
| 4 | Implementovat background task tracking | `GitHubWebhooksController.cs:173` | 2h |

### 7.2 Vysoké (P1) - Do 2 týdnů

| # | Akce | Soubor | Effort |
|---|------|--------|--------|
| 5 | Smazat dead code v TtsController | `TtsController.cs:45-156` | 15m |
| 6 | Rozdělit NotificationService | `NotificationService.cs` | 2h |
| 7 | Opravit race condition u provider creation | `NotificationService.cs:177-201` | 1h |
| 8 | Opravit lock() v async metodě | `AudioRecordingCoordinator.cs:47` | 30m |

### 7.3 Střední (P2) - Do měsíce

| # | Akce | Soubor | Effort |
|---|------|--------|--------|
| 9 | Přejmenovat ClipboardController route | `ClipboardController.cs:12` | 15m |
| 10 | Udělat VAD threshold konfigurovatelný | `VadService.cs:20` | 30m |
| 11 | Přidat unique index na Provider.Name | `ProviderConfiguration.cs` | 15m |
| 12 | Standardizovat DateTime → DateTimeOffset | Multiple entities | 1h |
| 13 | Konsolidovat duplicate DTOs | `DispatchTaskResult`, `DispatchTaskResponse` | 30m |
| 14 | Refaktorovat DictationWorker (SRP) | `DictationWorker.cs` | 4h |

### 7.4 Nízké (P3) - Backlog

| # | Akce | Soubor | Effort |
|---|------|--------|--------|
| 15 | Přidat OpenAPI dokumentaci na controllery | All controllers | 1h |
| 16 | Vytvořit unified ApiResponse wrapper | Multiple | 1h |
| 17 | Přidat global exception handler | Service layer | 1h |
| 18 | Dokumentovat FK delete policies | Configurations | 30m |
| 19 | Extrahovat factory creation z GetOrLoadModelAsync | `WhisperSpeechTranscriber.cs` | 1h |
| 20 | Refaktorovat SileroVadOnnxModel.Call() | `SileroVadOnnxModel.cs` | 2h |

### 7.5 Odložené (pouze pokud půjde API veřejně)

| # | Akce | Soubor | Poznámka |
|---|------|--------|----------|
| — | Vynutit webhook secret validaci | `GitHubWebhooksController.cs:52` | Pouze localhost, není potřeba |
| — | Přidat autentizaci na SignalR hub | `EndpointExtensions.cs:207` | Pouze localhost, není potřeba |

---

## 8. Závěr

VirtualAssistant je **solidní projekt** s dobrou architekturou, ale vyžaduje údržbu v několika oblastech:

### Silné stránky:
- Event-driven voice pipeline
- Circuit breaker pro TTS resilience
- CQRS pattern v data vrstvě
- Kvalitní testy v Service a Voice vrstvách
- Bezpečné secrets management (systemd env)

### Oblasti ke zlepšení:
- Unit test coverage pro controllery
- SRP violations v některých službách
- Memory management v VAD
- Dead code cleanup

### Doporučený přístup:
1. **Týden 1:** Stability issues - VAD buffer, audio truncation (P0)
2. **Týden 2-3:** Code quality - dead code, SRP refaktoring (P1)
3. **Měsíc:** Refaktoring a standardizace (P2)
4. **Ongoing:** Backlog items (P3)

### Poznámka k bezpečnosti:
API běží pouze na localhost (port 5055), takže webhook validace a SignalR autentizace nejsou aktuálně prioritou. Pokud by se v budoucnu API vystavovalo veřejně, viz sekce 7.5.

---

*Analýza vytvořena: 6. ledna 2026*  
*Nástroj: Claude Code (Sisyphus)*
