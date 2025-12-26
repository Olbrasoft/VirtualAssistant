# VirtualAssistant

Linux virtuální asistent pro ovládání desktopu a integraci s AI coding agenty.

## Funkce

- **Voice-to-OpenCode** – hlasové příkazy směrované do AI coding agenta
- **Kontinuální poslech** – 4 specializované workery (Audio Capture, VAD, Transcription, Action Executor)
- **VAD (Voice Activity Detection)** – Silero ONNX model pro detekci hlasu
- **Multi-provider LLM routing** – Groq, Cerebras, Mistral s automatickým fallbackem
- **Lokální ASR** – Whisper.NET s large-v3 modelem (FHS-compliant umístění)
- **Inter-agent komunikace** – Hub API pro komunikaci mezi AI agenty
- **Task Queue** – Automatická distribuce úkolů mezi agenty (ClaudeCode headless mode)
- **GitHub synchronizace** – Synchronizace issues s embeddings pro sémantické vyhledávání
- **TTS s fallbackem** – AzureTTS (primární), EdgeTTS, VoiceRSS, Google, Piper s circuit breaker pattern
- **DependentServicesManager** – Správa závislých služeb (TextToSpeech.Service)
- **Manuální mute** – Tlačítko myši pro dočasné ztlumení poslechu

## Architektura

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            VirtualAssistant                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Voice          │  VirtualAssistant.Service               │
│  - 4 background workers:         │  - ASP.NET Core API (port 5055)         │
│    • AudioCapturerWorker         │  - Tray ikona (GTK)                     │
│    • VoiceActivityWorker         │  - DependentServicesManager             │
│    • TranscriptionRouterWorker   │  - REST API controllers                 │
│    • ActionExecutorWorker        │                                         │
│  - TTS Provider Chain            │                                         │
│  - Whisper.NET (STT)             │                                         │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Core           │  VirtualAssistant.GitHub                │
│  - AgentHubService               │  - GitHub API integrace                 │
│  - AgentTaskService              │  - Synchronizace issues                 │
│  - TaskDistributionService       │  - Sémantické vyhledávání (pgvector)    │
│  - IManualMuteService            │  - Ollama embeddings                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Data           │  TextToSpeech.Service (External)        │
│  - Entity Framework Core         │  - Separate TTS service                 │
│  - PostgreSQL + pgvector         │  - Managed by DependentServicesManager  │
│  - CQRS handlers                 │                                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Note:** Push-to-Talk is now a [separate project](https://github.com/Olbrasoft/PushToTalk).

## Projekty

| Projekt | Popis |
|---------|-------|
| `VirtualAssistant.Core` | Business logic: AgentHubService, AgentTaskService, TaskDistributionService, DependentServicesManager, IManualMuteService |
| `VirtualAssistant.Voice` | 4 background workers, TTS provider chain, Whisper STT, LLM routing |
| `VirtualAssistant.Service` | ASP.NET Core hlavní služba s tray ikonou (port 5055) |
| `VirtualAssistant.Data` | Entity, enumy, DTO |
| `VirtualAssistant.Data.EntityFrameworkCore` | DbContext, konfigurace, migrace (auto-apply on startup) |
| `VirtualAssistant.GitHub` | GitHub API klient, synchronizace issues, embeddings (Ollama) |
| `VirtualAssistant.Tray` | Standalone tray aplikace (GTK) |
| `VirtualAssistant.Desktop` | Desktop komponenty |
| `VirtualAssistant.Plugins` | Plugin framework (placeholder) |
| `VirtualAssistant.Agent` | Agent modul (placeholder) |
| `VirtualAssistant.Api` | Minimal API (development) |

## API Endpointy

Služba běží na `http://localhost:5055`.

### Health Check

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| GET | `/health` | Health check |

### Notifications

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| POST | `/api/notifications` | Vytvoří notifikaci (ukládá do DB, přehraje přes TTS) |

### TTS (Text-to-Speech)

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| POST | `/api/tts/speak` | Pošle text k přečtení (source: opencode/claude/assistant) |
| GET | `/api/tts/queue` | Vrátí počet zpráv ve frontě |
| POST | `/api/tts/stop` | Zastaví aktuální přehrávání |
| POST | `/api/tts/flush-queue` | Přehraje všechny zprávy ve frontě |

### Assistant Speech (Echo Cancellation)

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| POST | `/api/assistant-speech/start` | TTS MCP server volá při začátku mluvení |
| POST | `/api/assistant-speech/end` | TTS MCP server volá při konci mluvení |
| GET | `/api/assistant-speech/status` | Stav historie pro echo cancellation |

### Mute Control

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| POST | `/api/mute` | Nastaví mute stav (mění ikonu tray) |
| GET | `/api/mute` | Vrátí aktuální mute stav |

### Agent Hub (Inter-agent Komunikace)

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| POST | `/api/hub/send` | Odešle zprávu jinému agentovi |
| GET | `/api/hub/pending/{agent}` | Nevyřízené zprávy pro agenta |
| POST | `/api/hub/approve/{id}` | Schválí zprávu čekající na schválení |
| POST | `/api/hub/cancel/{id}` | Zruší nevyřízenou zprávu |
| POST | `/api/hub/delivered/{id}` | Označí zprávu jako doručenou |
| POST | `/api/hub/processed/{id}` | Označí zprávu jako zpracovanou |
| GET | `/api/hub/queue` | Všechny zprávy ve frontě |
| GET | `/api/hub/awaiting-approval` | Zprávy čekající na schválení |
| POST | `/api/hub/start` | Zahájí nový task (s volitelným sessionId) |
| POST | `/api/hub/progress` | Progress update pro běžící task |
| POST | `/api/hub/complete` | Dokončí task |
| GET | `/api/hub/active` | Aktivní tasky (volitelně filtrované) |
| GET | `/api/hub/task/{taskId}` | Historie konkrétního tasku |

### Task Queue (Automatická Distribuce)

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| POST | `/api/tasks/create` | Vytvoří task (X-Agent-Name header) |
| GET | `/api/tasks/pending/{agent}` | Nevyřízené tasky pro agenta |
| GET | `/api/tasks/awaiting-approval` | Tasky čekající na schválení |
| GET | `/api/tasks/{taskId}` | Detail tasku |
| GET | `/api/tasks` | Všechny tasky (limit query param) |
| POST | `/api/tasks/{taskId}/approve` | Schválí task |
| POST | `/api/tasks/{taskId}/cancel` | Zruší task |
| POST | `/api/tasks/{taskId}/complete` | Dokončí task s výsledkem |
| GET | `/api/tasks/idle/{agent}` | Zjistí zda je agent nečinný |
| GET | `/api/tasks/ready-to-send` | Tasky připravené k odeslání |

### GitHub Synchronizace

| Metoda | Endpoint | Popis |
|--------|----------|-------|
| POST | `/api/github/sync/{owner}/{repo}` | Synchronizuje jedno repository |
| POST | `/api/github/sync/{owner}` | Synchronizuje všechna repositories vlastníka |
| GET | `/api/github/sync/status` | Stav synchronizace |
| POST | `/api/github/embeddings` | Generuje chybějící embeddings |
| GET | `/api/github/search?q=...` | Sémantické vyhledávání v issues |
| GET | `/api/github/duplicates?title=...` | Hledání duplicitních issues |
| GET | `/api/github/issues/open/{owner}/{repo}` | Otevřené issues repository |

## Databázové Schema

PostgreSQL databáze s pgvector extenzí pro sémantické vyhledávání.

### Tabulky

#### `agents`
Registrovaní AI agenti (opencode, claude).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| name | text | Identifikátor (opencode, claude) |
| label | text | GitHub label (agent:opencode) |
| is_active | bool | Je agent aktivní |
| created_at | timestamp | Datum vytvoření |

#### `agent_messages`
Zprávy mezi agenty (Hub API).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| source_agent | text | Zdrojový agent |
| target_agent | text | Cílový agent |
| message_type | text | Typ zprávy |
| content | text | Obsah zprávy |
| status | text | pending/approved/delivered/processed/cancelled |
| phase | int | Start=0, Progress=1, Complete=2 |
| session_id | text | ID relace (pro deduplikaci) |
| parent_message_id | int | FK na rodičovskou zprávu |
| requires_approval | bool | Vyžaduje schválení |
| created_at | timestamp | Vytvořeno |
| approved_at | timestamp | Schváleno |
| delivered_at | timestamp | Doručeno |
| processed_at | timestamp | Zpracováno |

#### `agent_tasks`
Task queue pro automatickou distribuci.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| github_issue_url | text | URL GitHub issue |
| github_issue_number | int | Číslo issue |
| summary | text | Popis úkolu |
| created_by_agent_id | int | FK na agents |
| target_agent_id | int | FK na agents |
| status | text | pending/approved/sent/completed/cancelled |
| requires_approval | bool | Vyžaduje schválení |
| result | text | Výsledek |
| created_at | timestamp | Vytvořeno |
| approved_at | timestamp | Schváleno |
| sent_at | timestamp | Odesláno |
| completed_at | timestamp | Dokončeno |

#### `agent_task_sends`
Log doručení tasků.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| task_id | int | FK na agent_tasks |
| agent_id | int | FK na agents |
| sent_at | timestamp | Čas odeslání |
| delivery_method | text | Způsob doručení (hub_api) |
| response | text | Odpověď |

#### `github_repositories`
Synchronizovaná GitHub repositories.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| owner | text | Vlastník |
| name | text | Název |
| full_name | text | Plný název (owner/name) |
| synced_at | timestamp | Poslední synchronizace |

#### `github_issues`
Synchronizované GitHub issues s embeddings.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| repository_id | int | FK na github_repositories |
| issue_number | int | Číslo issue |
| title | text | Název |
| body | text | Popis |
| state | text | open/closed |
| html_url | text | URL |
| title_embedding | vector(1536) | Embedding titulku |
| body_embedding | vector(1536) | Embedding popisu |
| embedding_generated_at | timestamp | Kdy generováno |

#### `github_issue_agents`
Přiřazení agentů k issues.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| github_issue_id | int | FK na github_issues |
| agent_label | text | Label agenta |

#### `voice_transcriptions`
Historie hlasových přepisů.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| transcribed_text | text | Přepsaný text |
| source_app | text | Aktivní aplikace |
| duration_ms | int | Délka nahrávky |
| created_at | timestamp | Vytvořeno |

#### `system_startups`
Log startů systému.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| startup_type | int | Typ startu |
| started_at | timestamp | Čas startu |
| ended_at | timestamp | Čas ukončení |
| shutdown_type | int | Typ ukončení |

## Požadavky

- .NET 10
- Linux (testováno na Debian 13, GNOME)
- PostgreSQL 16+ s pgvector extension
- PipeWire/PulseAudio (audio capture)
- Whisper model (`ggml-large-v3.bin`) v `~/.local/share/whisper-models/` (FHS-compliant)
- Silero VAD model (`silero_vad.onnx`) v `/opt/olbrasoft/virtual-assistant/models/`
- Ollama (embeddings pro GitHub search)

## Instalace

```bash
# Klonování
git clone https://github.com/Olbrasoft/VirtualAssistant.git
cd VirtualAssistant

# Build
dotnet build

# Testy (bez integračních testů)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
```

**Note:** Migrace databáze se aplikují automaticky při startu služby.

## Konfigurace

### Development Configuration

`src/VirtualAssistant.Service/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "VirtualAssistantDb": "Host=localhost;Database=virtual_assistant;Username=user;Password=pass"
  },
  "ListenerApiPort": 5055,
  "OpenCodeUrl": "http://localhost:4096",
  "Audio": {
    "WhisperModelPath": "~/.local/share/whisper-models/ggml-large-v3.bin",
    "SileroVadModelPath": "/opt/olbrasoft/virtual-assistant/models/silero_vad.onnx"
  },
  "TtsProviderChain": {
    "Providers": ["AzureTTS", "HttpEdgeTts", "VoiceRss", "Google", "Piper"]
  }
}
```

### Production Secrets

**CRITICAL:** Production secrets are in systemd EnvironmentFile:

`~/.config/systemd/user/virtual-assistant.env`:
```bash
# Azure TTS (Primary provider)
AzureTTS__SubscriptionKey=xxxxx
AZURE_SPEECH_REGION=westeurope

# GitHub
GitHub__Token=ghp_xxxxx

# LLM Providers
GROQ_API_KEY=xxxxx
CEREBRAS_API_KEY=xxxxx
MISTRAL_API_KEY=xxxxx
```

### Voice Prompts

System prompts v `/opt/olbrasoft/virtual-assistant/app/Prompts/`:
- `VoiceRouterSystem.md` – hlavní system prompt pro LLM routing
- `DiscussionActiveWarning.md` – varování pro diskuzní mód

## Deployment

```bash
# RECOMMENDED: Always use deployment script
cd ~/Olbrasoft/VirtualAssistant
./deploy/deploy.sh /opt/olbrasoft/virtual-assistant

# Manual deploy (emergency only - script is safer!)
dotnet publish src/VirtualAssistant.Service/VirtualAssistant.Service.csproj \
  -c Release \
  -o /opt/olbrasoft/virtual-assistant/app \
  --no-self-contained

# Copy config (without secrets!)
cp src/VirtualAssistant.Service/appsettings.json /opt/olbrasoft/virtual-assistant/config/

# Systemd služby
systemctl --user daemon-reload
systemctl --user enable virtual-assistant.service
systemctl --user start virtual-assistant.service

# Verify deployment
systemctl --user status virtual-assistant
curl http://localhost:5055/health

# Check logs for errors (especially "not configured")
journalctl --user -u virtual-assistant -n 50
```

**Production directory structure:**
```
/opt/olbrasoft/virtual-assistant/
├── app/                 # Binaries (from dotnet publish)
├── config/              # appsettings.json (NO secrets!)
├── data/                # Runtime data, databases
├── models/              # silero_vad.onnx
└── icons/               # Tray icons
```

**Secrets:** `~/.config/systemd/user/virtual-assistant.env` (loaded via systemd EnvironmentFile)

## Testování

```bash
# Všechny testy
dotnet test

# Konkrétní projekt
dotnet test tests/VirtualAssistant.Voice.Tests
dotnet test tests/VirtualAssistant.Core.Tests
```

**Aktuální stav:** Testy procházejí.

## LLM Providers

Voice router podporuje více LLM providerů s automatickým fallbackem při rate limitech:

1. **Groq** (primární) – nejrychlejší, `llama-3.3-70b-versatile`
2. **Cerebras** (fallback) – `llama-3.3-70b`
3. **Mistral** (fallback) – `mistral-large-latest`

## TTS Provider Chain (Circuit Breaker Pattern)

Systém podporuje automatický fallback mezi TTS providery:

1. **AzureTTS** (primární) – Azure Speech Service (0.5M chars/month free tier)
2. **HttpEdgeTts** (fallback) – Microsoft Edge TTS přes WebSocket server (`http://localhost:5555`)
3. **VoiceRss** (fallback) – VoiceRSS cloud TTS
4. **Google** (fallback) – Google Cloud TTS
5. **Piper** (fallback) – lokální offline syntéza

### Circuit Breaker Behavior

- Provider se přeskočí po 3 neúspěšných pokusech (`MaxConsecutiveFailures`)
- Circuit breaker se resetuje po 300 sekundách (`CircuitBreakerTimeoutSeconds`)
- Automatický fallback na další provider v řetězci

### Azure TTS (Primary Provider)

**Credentials:** `~/.config/systemd/user/virtual-assistant.env`
```bash
AzureTTS__SubscriptionKey=xxxxx
AZURE_SPEECH_REGION=westeurope
```

**Výhody:**
- 0.5M znaků/měsíc zdarma
- Vysoká kvalita hlasu
- Rychlá odezva

### EdgeTTS Fallback Server

```bash
# Install
pip install edge-tts

# Run server
edge-tts-server --port 5555

# Or via systemd
systemctl --user start edge-tts-server
```

## Background Services

### Voice Workers (4 specialized workers)

#### 1. AudioCapturerWorker
- Kontinuální audio capture přes PipeWire (pw-record)
- Respektuje mute stav (IManualMuteService)
- Publikuje AudioChunkCapturedEvent s RMS kalkulací

#### 2. VoiceActivityWorker
- Voice Activity Detection (Silero VAD ONNX)
- Detekuje začátek a konec řeči
- Publikuje VoiceActivityDetectedEvent

#### 3. TranscriptionRouterWorker
- Whisper.NET transcription (large-v3 model)
- LLM routing (Groq → Cerebras → Mistral fallback)
- Rozhodování o akci (opencode, respond, ignore, savenote, etc.)

#### 4. ActionExecutorWorker
- Provádí akce z LLM rozhodnutí
- Volá OpenCode API, TTS, nebo ukládá poznámky

### TaskDistributionService

Automaticky distribuuje schválené úkoly nečinným agentům (každých 10s).

Workflow:
1. Agent vytvoří task přes `/api/tasks/create`
2. Uživatel schválí přes `/api/tasks/{id}/approve` (pokud `requires_approval=true`)
3. TaskDistributionService zjistí že cílový agent je nečinný
4. Odešle task přes `/api/hub/dispatch-task`
5. Pro ClaudeCode: headless mode (`claude -p "..." --output-format json`)
6. Notifikuje uživatele přes TTS

### GitHubSyncBackgroundService

Periodicky synchronizuje GitHub issues a generuje embeddings (Ollama).

## Licence

MIT License – viz [LICENSE](LICENSE)
