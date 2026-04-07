# VirtualAssistant

Linux virtuální asistent pro ovládání desktopu a integraci s AI coding agenty Claude Code, OpenCode a Gemini CLI.

## Funkce

- **Voice-to-OpenCode** – hlasové příkazy směrované do AI coding agenta
- **Multi-Agent Support** – Claude Code, OpenCode, Gemini CLI s různými hlasy (mužský Antonín/ženský Vlasta)
- **Kontinuální poslech** – 4 specializované workery (Audio Capture, VAD, Transcription, Action Executor)
- **VAD (Voice Activity Detection)** – Silero ONNX model pro detekci hlasu
- **Multi-provider LLM routing** – Groq, Cerebras, Mistral s automatickým fallbackem
- **Lokální ASR** – Whisper.NET s large-v3 modelem (FHS-compliant umístění)
- **GitHub synchronizace** – Synchronizace issues s embeddings pro sémantické vyhledávání (Ollama nomic-embed-text)
- **TTS s fallbackem** – AzureTTS (primární), EdgeTTS, VoiceRSS, Google, Piper s circuit breaker pattern
- **Desktop Context Awareness** – GNOME integration pro context-aware LLM prompty
- **Manuální mute** – Tlačítko myši pro dočasné ztlumení poslechu

## Agent Integration

VirtualAssistant podporuje notifikace z více AI agentů s rozlišením hlasů pomocí TTS profilů:

```
┌───────────────────────────────────────────────────────────────────────────┐
│                     Notification Sources                                  │
├───────────────────────────────────────────────────────────────────────────┤
│  Claude Code (MCP mcp-notify) ────────► Agent: claude-code (male voice)  │
│  OpenCode (plugin notify.js) ─────────► Agent: opencode (male voice)     │
│  Gemini CLI (MCP mcp-notify-gemini) ──► Agent: gemini (female voice)     │
├───────────────────────────────────────────────────────────────────────────┤
│                      Voice Selection                                      │
│  • TtsProfiles configuration maps agent name to voice                     │
│  • Azure TTS primary provider (Antonín male / Vlasta female)             │
│  • Fallback chain: EdgeTTS → VoiceRss → Google → Piper                   │
└───────────────────────────────────────────────────────────────────────────┘
```

| Agent | Voice | Provider | Agent ID |
|-------|-------|----------|----------|
| claude-code | cs-CZ-AntoninNeural (male) | Azure | 4 |
| opencode | cs-CZ-AntoninNeural (male) | Azure | 1 |
| gemini | cs-CZ-VlastaNeural (female) | Azure | 11 |

## Architektura

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            VirtualAssistant                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Voice          │  VirtualAssistant.Service               │
│  - 4 background workers:         │  - ASP.NET Core API (port 5055)         │
│    • AudioCapturerWorker         │  - Tray ikona (GTK)                     │
│    • VoiceActivityWorker         │  - REST API controllers                 │
│    • TranscriptionRouterWorker   │  - SignalR Hub (Desktop Monitor)        │
│    • ActionExecutorWorker        │                                         │
│  - TTS Provider Chain            │                                         │
│  - Whisper.NET (inline STT)      │                                         │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Core           │  VirtualAssistant.GitHub                │
│  - NotificationService           │  - GitHub API integrace                 │
│  - DictationPersistenceService   │  - Synchronizace issues                 │
│  - DesktopContextService         │  - Sémantické vyhledávání (pgvector)    │
│  - IManualMuteService            │  - Ollama embeddings (nomic-embed-text) │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Data           │  VirtualAssistant.Desktop               │
│  - Entity Framework Core         │  - GNOME LinuxDesktop integration       │
│  - PostgreSQL + pgvector         │  - Desktop context awareness            │
│  - CQRS queries/commands         │  - Window/workspace tracking            │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Note:** Push-to-Talk is now a [separate project](https://github.com/Olbrasoft/PushToTalk).

## Projekty

| Projekt | Popis |
|---------|-------|
| `VirtualAssistant.Service` | ASP.NET Core hlavní služba s tray ikonou (port 5055) |
| `VirtualAssistant.Core` | Business logic services, NotificationService, DictationPersistenceService |
| `VirtualAssistant.Voice` | 4 background workers, TTS provider chain, Whisper STT inline, VAD |
| `VirtualAssistant.Data` | CQRS queries, commands, entities, DTOs, enums |
| `VirtualAssistant.Data.EntityFrameworkCore` | DbContext, query/command handlers, migrations (auto-apply on startup) |
| `VirtualAssistant.GitHub` | GitHub API klient, synchronizace issues, embeddings (Ollama nomic-embed-text 768d) |
| `VirtualAssistant.LlmChain` | Multi-provider LLM routing (Groq, Cerebras, Mistral, OpenRouter) with circuit breaker |
| `VirtualAssistant.Desktop` | Desktop context awareness (GNOME LinuxDesktop integration) |
| `VirtualAssistant.Api` | Minimal API endpoints |

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
| POST | `/api/tts/speak` | Pošle text k přečtení (source: claude-code/opencode/gemini/assistant) |
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

## Příklady Použití

### Notifikace z různých agentů

```bash
# Claude Code - mužský hlas (Antonín)
curl -X POST http://localhost:5055/api/notifications \
  -H "Content-Type: application/json" \
  -d '{"text": "Build dokončen úspěšně", "source": "claude-code", "issueIds": [123]}'

# OpenCode - mužský hlas (Antonín)
curl -X POST http://localhost:5055/api/notifications \
  -H "Content-Type: application/json" \
  -d '{"text": "Refactoring hotový", "source": "opencode"}'

# Gemini CLI - ženský hlas (Vlasta)
curl -X POST http://localhost:5055/api/notifications \
  -H "Content-Type: application/json" \
  -d '{"text": "Analýza kódu dokončena", "source": "gemini", "issueIds": [456]}'
```

### Přímé TTS

```bash
# TTS s výběrem hlasu podle agenta
curl -X POST http://localhost:5055/api/tts/speak \
  -H "Content-Type: application/json" \
  -d '{"text": "Testovací zpráva", "source": "gemini"}'
```

## Databázové Schema

PostgreSQL databáze s pgvector extenzí pro sémantické vyhledávání.

### Tabulky

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
| title_embedding | vector(768) | Embedding titulku (nomic-embed-text) |
| body_embedding | vector(768) | Embedding popisu (nomic-embed-text) |
| embedding_generated_at | timestamp | Kdy generováno |

#### `voice_transcriptions`
Historie hlasových přepisů (dictation mode).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| transcribed_text | text | Přepsaný text |
| source_app | text | Aktivní aplikace během diktování |
| duration_ms | int | Délka nahrávky v ms |
| created_at | timestamp | Vytvořeno |

#### `whisper_transcriptions`
Historie Whisper AI přepisů (continuous listening).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| transcribed_text | text | Přepsaný text z Whisper AI |
| audio_duration_ms | int | Délka audio záznamu v ms |
| created_at | timestamp | Vytvořeno |

#### `notifications`
Notifikace od agentů.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| text | text | Text notifikace |
| agent_id | int | FK na agents (zdroj notifikace) |
| notification_status_id | int | FK na notification_statuses |
| created_at | timestamp | Vytvořeno |
| final_provider_id | int | FK na providers (použitý TTS provider) |
| final_tts_status | text | success/error/timeout/all_failed |
| tts_completed_at | timestamp | Kdy dokončeno TTS |

#### `notification_statuses`
Statusy notifikací.

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| name | text | pending/processing/played |

#### `providers`
TTS providery (AzureTTS, EdgeTTS, VoiceRss, Google, Piper).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| name | text | Název providera |

#### `notification_tts_attempts`
Log TTS pokusů pro notifikace (circuit breaker tracking).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| notification_id | int | FK na notifications |
| provider_id | int | FK na providers |
| attempt_order | int | Pořadí pokusu (1, 2, 3...) |
| status | text | success/error/timeout |
| error_message | text | Chybová zpráva |
| attempted_at | timestamp | Kdy pokus |

#### `transcription_corrections`
Slovník oprav pro Whisper přepisy (case-insensitive).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| incorrect_text | text | Špatný text z Whisper |
| correct_text | text | Správný text |
| is_active | bool | Je oprava aktivní |
| priority | int | Priorita (vyšší = dříve aplikováno) |
| created_at | timestamp | Vytvořeno |
| updated_at | timestamp | Aktualizováno |

#### `transcription_correction_usages`
Sledování použití oprav (analytics).

| Sloupec | Typ | Popis |
|---------|-----|-------|
| id | int | PK |
| transcription_correction_id | int | FK na transcription_corrections |
| used_at | timestamp | Kdy použito |
| context | text | Kontext (dictation, continuous-listening) |

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

**4 Voice Workers** (pipelined architecture):
1. **AudioCapturerWorker** - Continuous audio capture via PipeWire (respects mute state)
2. **VoiceActivityWorker** - VAD detection using Silero ONNX model
3. **TranscriptionRouterWorker** - Whisper.NET transcription + LLM routing (Groq → Cerebras → Mistral fallback)
4. **ActionExecutorWorker** - Executes actions from LLM decisions (OpenCode API, TTS, save notes)

**Other Background Workers:**
- **GitHubSyncBackgroundService** - Periodic GitHub issue sync with embeddings (Ollama nomic-embed-text)
- **DesktopMonitorBroadcastWorker** - Broadcasts desktop context changes via SignalR (real-time dashboard)

## Licence

MIT License – viz [LICENSE](LICENSE)
