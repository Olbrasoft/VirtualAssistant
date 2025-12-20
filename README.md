# VirtualAssistant

Linux virtuální asistent pro ovládání desktopu a integraci s AI coding agenty.

## Funkce

- **Voice-to-OpenCode** – hlasové příkazy směrované do AI coding agenta
- **Push-to-Talk diktování** – přepis hlasu do aktivního okna
- **Kontinuální poslech** – VAD (Voice Activity Detection) s Silero ONNX
- **Multi-provider LLM routing** – Groq, Cerebras, Mistral s automatickým fallbackem
- **Lokální ASR** – WhisperNet s large-v3 modelem
- **Inter-agent komunikace** – Hub API pro komunikaci mezi AI agenty
- **Task Queue** – Automatická distribuce úkolů mezi agenty
- **GitHub synchronizace** – Synchronizace issues s embeddings pro sémantické vyhledávání
- **TTS notifikace** – Text-to-speech notifikace s rozlišením zdroje

## Architektura

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            VirtualAssistant                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Voice          │  VirtualAssistant.PushToTalk            │
│  - Kontinuální poslech           │  - PTT diktování                        │
│  - VAD (Silero ONNX)             │  - Whisper ASR                          │
│  - LLM routing                   │  - Text typing (xdotool)                │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Core           │  VirtualAssistant.Service               │
│  - AgentHubService               │  - Systemd worker (port 5055)           │
│  - AgentTaskService              │  - Tray ikona (GTK)                     │
│  - TaskDistributionService       │  - REST API endpointy                   │
├─────────────────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Data           │  VirtualAssistant.GitHub                │
│  - Entity Framework Core         │  - GitHub API integrace                 │
│  - PostgreSQL + pgvector         │  - Synchronizace issues                 │
│  - CQRS handlers                 │  - Sémantické vyhledávání               │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Projekty

| Projekt | Popis |
|---------|-------|
| `VirtualAssistant.Core` | Sdílené služby: AgentHubService, AgentTaskService, TaskDistributionService |
| `VirtualAssistant.Voice` | Kontinuální poslech, VAD, LLM routing, TTS |
| `VirtualAssistant.PushToTalk` | Push-to-Talk knihovna |
| `VirtualAssistant.PushToTalk.Service` | PTT služba (port 5050) |
| `VirtualAssistant.Service` | Hlavní služba s tray ikonou (port 5055) |
| `VirtualAssistant.Data` | Entity, enumy, DTO |
| `VirtualAssistant.Data.EntityFrameworkCore` | DbContext, konfigurace, migrace |
| `VirtualAssistant.GitHub` | GitHub API klient, synchronizace issues |
| `VirtualAssistant.Api` | REST API endpoint |
| `VirtualAssistant.Tray` | Standalone tray aplikace |
| `VirtualAssistant.Agent` | AI agent (WIP) |
| `VirtualAssistant.Desktop` | Desktop integrace (WIP) |
| `VirtualAssistant.Plugins` | Plugin systém (WIP) |

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
- Linux (testováno na Debian 13)
- PostgreSQL 16+ s pgvector
- ALSA nebo PulseAudio
- xdotool / dotool (pro text input)
- Whisper model (`ggml-large-v3.bin`)
- Silero VAD model (`silero_vad.onnx`)

## Instalace

```bash
# Klonování
git clone https://github.com/Olbrasoft/VirtualAssistant.git
cd VirtualAssistant

# Build
dotnet build

# Testy
dotnet test

# Migrace databáze
cd src/VirtualAssistant.Service
dotnet ef database update
```

## Konfigurace

### Connection String

`src/VirtualAssistant.Service/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "VirtualAssistantDb": "Host=localhost;Database=virtual_assistant;Username=user;Password=pass"
  },
  "ListenerApiPort": 5055,
  "OpenCodeUrl": "http://localhost:4096"
}
```

### Push-to-Talk služba

`src/VirtualAssistant.PushToTalk.Service/appsettings.json`:

```json
{
  "Whisper": {
    "ModelPath": "/cesta/k/ggml-large-v3.bin"
  },
  "PushToTalk": {
    "DevicePath": "/dev/input/event0"
  }
}
```

### Voice služba

Prompty jsou v `src/VirtualAssistant.Voice/Prompts/`:
- `VoiceRouterSystem.md` – hlavní system prompt pro routing
- `DiscussionActiveWarning.md` – varování pro diskuzní mód

## Deployment

```bash
# Deploy hlavní služby - RECOMMENDED: Use ./deploy/deploy.sh instead
dotnet publish src/VirtualAssistant.Service/VirtualAssistant.Service.csproj \
  -c Release -o ~/apps/virtual-assistant --no-self-contained

# Deploy Push-to-Talk služby
dotnet publish src/VirtualAssistant.PushToTalk.Service/VirtualAssistant.PushToTalk.Service.csproj \
  -c Release -o ~/apps/virtual-assistant/push-to-talk --no-self-contained

# Systemd služby
systemctl --user enable virtual-assistant.service
systemctl --user start virtual-assistant.service

systemctl --user enable push-to-talk-dictation.service
systemctl --user start push-to-talk-dictation.service
```

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

## TTS (Text-to-Speech) Fallback

Systém podporuje automatický fallback pro TTS:

1. **EdgeTTS** (primární) – Microsoft Edge TTS přes `http://localhost:5555`
2. **Piper TTS** (fallback) – lokální offline syntéza s českým hlasem `cs_CZ-jirka-medium`

### Piper TTS Fallback

Když EdgeTTS selže (např. Microsoft WebSocket problémy), automaticky se použije Piper:

- Model: `/home/jirka/apps/virtual-assistant/piper-voices/cs/cs_CZ-jirka-medium.onnx`
- Respektuje CapsLock stav (stejný algoritmus jako EdgeTTS):
  - Kontroluje před spuštěním syntézy
  - Kontroluje po generování ale před přehráváním
  - Polluje každých 100ms během přehrávání a okamžitě zastaví při stisku CapsLock

### Instalace Piper

```bash
pipx install piper-tts

# Stáhnout český model
mkdir -p ~/apps/virtual-assistant/piper-voices/cs
cd ~/apps/virtual-assistant/piper-voices/cs
wget https://huggingface.co/rhasspy/piper-voices/resolve/main/cs/cs_CZ/jirka/medium/cs_CZ-jirka-medium.onnx
wget https://huggingface.co/rhasspy/piper-voices/resolve/main/cs/cs_CZ/jirka/medium/cs_CZ-jirka-medium.onnx.json
```

## Background Services

### TaskDistributionService

Automaticky distribuuje schválené úkoly nečinným agentům. Kontroluje každých 10 sekund.

Workflow:
1. Agent vytvoří task přes `/api/tasks/create`
2. Uživatel schválí přes `/api/tasks/{id}/approve` (pokud `requires_approval=true`)
3. TaskDistributionService zjistí že cílový agent je nečinný
4. Odešle task přes Hub API (`/api/hub/start`)
5. Notifikuje uživatele přes TTS

### GitHubSyncBackgroundService

Periodicky synchronizuje GitHub issues a generuje embeddings.

## Licence

MIT License – viz [LICENSE](LICENSE)
