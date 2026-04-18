# VirtualAssistant Architecture

> Comprehensive architecture documentation for the VirtualAssistant project.

## Overview

VirtualAssistant is a **Linux voice-controlled virtual assistant** built on .NET 10 with:

- **Clean Architecture** with CQRS pattern
- **Event-driven pipeline** for voice processing
- **Circuit breaker pattern** for TTS provider chain
- **Semantic search** with pgvector embeddings

---

## Project Structure

```
~/Olbrasoft/VirtualAssistant/
├── src/                                    # 8 production projects
│   ├── VirtualAssistant.Service            # ASP.NET Core host (port 5055) - ENTRY POINT
│   ├── VirtualAssistant.Core               # Business logic, services, events
│   ├── VirtualAssistant.Voice              # Audio capture, VAD, Whisper STT, TTS
│   ├── VirtualAssistant.Data               # Entities, DTOs, CQRS commands/queries
│   ├── VirtualAssistant.Data.EntityFrameworkCore  # DbContext, migrations, handlers
│   ├── VirtualAssistant.Desktop            # GNOME integration, desktop context
│   ├── VirtualAssistant.GitHub             # GitHub API, embeddings (pgvector)
│   └── VirtualAssistant.LlmChain           # Multi-provider LLM routing
│
├── tests/                                  # 7 test projects (xUnit + Moq)
│   ├── VirtualAssistant.Core.Tests
│   ├── VirtualAssistant.Voice.Tests
│   ├── VirtualAssistant.Service.Tests
│   ├── VirtualAssistant.Data.EntityFrameworkCore.Tests
│   ├── VirtualAssistant.Desktop.Tests
│   ├── VirtualAssistant.GitHub.Tests
│   └── VirtualAssistant.LlmChain.IntegrationTests
│
├── deploy/                                 # Deployment scripts
└── docs/                                   # Documentation
```

---

## Dependency Graph

```
                         ┌─────────────────────────────┐
                         │   VirtualAssistant.Service  │ ◄── Entry Point
                         │   (ASP.NET Core, Tray)      │
                         └──────────────┬──────────────┘
                                        │
           ┌────────────────────────────┼────────────────────────────┐
           │                            │                            │
           ▼                            ▼                            ▼
   ┌───────────────┐           ┌───────────────┐           ┌───────────────┐
   │    .Voice     │           │   .Desktop    │           │   .GitHub     │
   │ ─────────────│           │ ─────────────│           │ ─────────────│
   │ • Audio      │           │ • GNOME DBus  │           │ • GitHub API  │
   │ • VAD (ONNX) │           │ • Window      │           │ • Issue Sync  │
   │ • Whisper    │           │   Tracking    │           │ • Embeddings  │
   │ • TTS Chain  │           │ • Workspace   │           │ • pgvector    │
   └───────┬───────┘           └───────────────┘           └───────────────┘
           │
           ▼
   ┌───────────────┐           ┌───────────────┐
   │    .Core      │──────────▶│  .LlmChain    │
   │ ─────────────│           │ ─────────────│
   │ • Services   │           │ • Groq        │
   │ • Events     │           │ • Cerebras    │
   │ • Interfaces │           │ • Mistral     │
   └───────┬───────┘           └───────────────┘
           │
           ▼
   ┌───────────────┐           ┌─────────────────────────────┐
   │    .Data      │──────────▶│ .Data.EntityFrameworkCore   │
   │ ─────────────│           │ ─────────────────────────── │
   │ • Entities   │           │ • VirtualAssistantDbContext │
   │ • DTOs       │           │ • Migrations (auto-apply)   │
   │ • Commands   │           │ • Query/Command Handlers    │
   │ • Queries    │           │ • EF Configurations         │
   └───────────────┘           └─────────────────────────────┘
```

---

## Architectural Patterns

| Layer | Pattern | Description |
|-------|---------|-------------|
| **Data** | CQRS | Commands/Queries separation using Olbrasoft.Data.Cqrs |
| **Core** | Domain Services | Business logic with interface abstractions |
| **Voice** | Event-Driven Pipeline | 4 workers connected via events |
| **Service** | Composition Root | DI registration, Extensions pattern |
| **GitHub** | Repository + Embeddings | pgvector for semantic search |
| **TTS** | Circuit Breaker | Chain with automatic fallback |
| **LLM** | Chain of Responsibility | Multi-provider with rate limit fallback |

---

## Voice Processing Pipeline

The voice processing uses an **event-driven pipeline** with 4 specialized workers:

```
┌─────────────────────────┐
│   AudioCapturerWorker   │
│   ───────────────────   │
│   • PipeWire capture    │
│   • Mute-aware          │
│   • RMS calculation     │
└───────────┬─────────────┘
            │ AudioChunkCapturedEvent
            ▼
┌─────────────────────────┐
│   VoiceActivityWorker   │
│   ───────────────────   │
│   • Silero VAD (ONNX)   │
│   • Speech detection    │
│   • Silence detection   │
└───────────┬─────────────┘
            │ VoiceActivityDetectedEvent
            ▼
┌─────────────────────────────┐
│  TranscriptionRouterWorker  │
│  ─────────────────────────  │
│  • Whisper.NET (GPU)        │
│  • LLM routing decision     │
│  • Groq → Cerebras → Mistral│
└───────────┬─────────────────┘
            │ ActionDecisionEvent
            ▼
┌─────────────────────────┐
│   ActionExecutorWorker  │
│   ───────────────────   │
│   • OpenCode API call   │
│   • TTS response        │
│   • Save note           │
└─────────────────────────┘
```

### Event Flow

1. **AudioCapturerWorker** captures audio via PipeWire (respects mute state)
2. **VoiceActivityWorker** runs Silero VAD ONNX model for speech detection
3. **TranscriptionRouterWorker** transcribes with Whisper.NET and routes via LLM
4. **ActionExecutorWorker** executes the decided action (opencode/respond/ignore/savenote)

---

## TTS Provider Chain (Circuit Breaker)

```
┌──────────────────────────┐
│  IVirtualAssistantSpeaker │
└───────────┬──────────────┘
            │
            ▼
┌──────────────────────────┐
│    ITtsProviderChain     │ ◄── Circuit Breaker Pattern
│    ──────────────────    │
│    MaxFailures: 3        │
│    ResetTimeout: 300s    │
└───────────┬──────────────┘
            │
   ┌────────┴────────┬────────────┬────────────┬──────────┐
   ▼                 ▼            ▼            ▼          ▼
┌───────┐       ┌────────┐   ┌─────────┐   ┌────────┐  ┌───────┐
│ Azure │──X──▶│ EdgeTTS │──▶│ VoiceRss│──▶│ Google │─▶│ Piper │
│ (1st) │      │  (2nd)  │   │  (3rd)  │   │ (4th)  │  │ (5th) │
└───────┘       └────────┘   └─────────┘   └────────┘  └───────┘
```

**Behavior:**
- Primary: AzureTTS (0.5M chars/month free tier)
- Automatic fallback on provider failure
- Circuit opens after 3 consecutive failures
- Circuit resets after 300 seconds

---

## Key Services

### Core Layer

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `NotificationService` | `INotificationService` | Save notifications to DB, trigger TTS |
| `SettingsService` | `ISettingsService` | Application settings management |
| `TextInputService` | `ITextInputService` | Text input via clipboard/keyboard |

### Voice Layer

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `TtsService` | - | TTS with provider chain and caching |
| `TranscriptionService` | `ITranscriptionService` | Whisper.NET inline STT |
| `VadService` | `IVadService` | Silero VAD ONNX inference |
| `AudioCaptureService` | `IAudioCaptureService` | PipeWire audio capture |
| `ManualMuteService` | `IManualMuteService` | Mouse button mute control |

### Service Layer

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `ClaudeDispatchService` | `IClaudeDispatchService` | Dispatch tasks to Claude headless |
| `ServiceLifecycleManager` | `IServiceLifecycleManager` | Manage dependent services |
| `WorkspaceDetectionService` | `IWorkspaceDetectionService` | Detect current workspace |

### GitHub Layer

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `GitHubReferenceService` | `IGitHubReferenceService` | Sync issues, generate embeddings |
| `GitHubIssueStatusService` | `IGitHubIssueStatusService` | Issue status tracking |

---

## Database Schema (PostgreSQL + pgvector)

### Entity Relationship Diagram

```
┌─────────────────────┐       ┌────────────────────────┐
│ github_repositories │       │     github_issues      │
│ ─────────────────── │       │ ────────────────────── │
│ id (PK)             │◀──────│ repository_id (FK)     │
│ owner               │       │ issue_number           │
│ name                │       │ title                  │
│ full_name           │       │ body                   │
│ synced_at           │       │ state                  │
└─────────────────────┘       │ title_embedding (768d) │
                              │ body_embedding (768d)  │
                              └────────────────────────┘

┌─────────────────────┐       ┌────────────────────────┐
│       agents        │       │     notifications      │
│ ─────────────────── │       │ ────────────────────── │
│ id (PK)             │◀──────│ agent_id (FK)          │
│ name                │       │ text                   │
│ label               │       │ notification_status_id │
│ is_active           │       │ final_provider_id      │
└─────────────────────┘       │ final_tts_status       │
                              └───────────┬────────────┘
                                          │
                              ┌───────────▼────────────┐
                              │ notification_tts_attempts │
                              │ ─────────────────────────│
                              │ notification_id (FK)     │
                              │ provider_id (FK)         │
                              │ attempt_order            │
                              │ status                   │
                              └──────────────────────────┘

┌─────────────────────────┐   ┌────────────────────────────┐
│  whisper_transcriptions │   │ transcription_corrections  │
│ ─────────────────────── │   │ ──────────────────────────│
│ id (PK)                 │   │ id (PK)                    │
│ transcribed_text        │   │ incorrect_text             │
│ audio_duration_ms       │   │ correct_text               │
│ created_at              │   │ is_active                  │
└─────────────────────────┘   │ priority                   │
                              └────────────────────────────┘
```

### Key Tables

| Table | Purpose |
|-------|---------|
| `github_issues` | Synced issues with vector embeddings for semantic search |
| `whisper_transcriptions` | Continuous listening transcription history |
| `voice_transcriptions` | Dictation mode transcription history |
| `notifications` | TTS notifications with attempt tracking |
| `transcription_corrections` | Whisper correction dictionary |
| `prompts` | Context-aware LLM prompts |

---

## Technology Stack

| Category | Technology | Version |
|----------|------------|---------|
| **Runtime** | .NET | 10.0 |
| **Database** | PostgreSQL + pgvector | 16+ |
| **ORM** | Entity Framework Core | 10.0 |
| **STT** | Whisper.NET (CUDA GPU) | 1.9.0 |
| **VAD** | Silero ONNX | via ONNX Runtime 1.21.0 |
| **TTS** | Azure, EdgeTTS, VoiceRss, Google, Piper | various |
| **LLM** | Groq, Cerebras, Mistral | via HTTP API |
| **Embeddings** | Ollama (nomic-embed-text) | 768 dimensions |
| **Audio** | NAudio, PipeWire | 2.2.1 |
| **Desktop** | GNOME DBus (Tmds.DBus.Protocol) | - |
| **Testing** | xUnit + Moq | - |

---

## API Endpoints

### Health & System
```
GET  /health              # Health check
GET  /api/mute            # Get mute state
POST /api/mute            # Set mute state
```

### TTS (Text-to-Speech)
```
POST /api/tts/speak       # Speak text
POST /api/tts/stop        # Stop current playback
POST /api/tts/flush-queue # Flush TTS queue
GET  /api/tts/queue       # Get queue count
```

### GitHub Integration
```
GET  /api/github/search?q=...           # Semantic search
GET  /api/github/duplicates?title=...   # Find duplicates
POST /api/github/sync/{owner}/{repo}    # Sync repository
POST /api/github/embeddings             # Generate embeddings
```

### Notifications
```
POST /api/notifications   # Create notification (saves to DB, plays TTS)
```

---

## Configuration

### Development (appsettings.json)
```json
{
  "ConnectionStrings": {
    "VirtualAssistantDb": "Host=localhost;Database=virtual_assistant;..."
  },
  "ListenerApiPort": 5055,
  "OpenCodeUrl": "http://localhost:4096",
  "Audio": {
    "WhisperModelPath": "~/.local/share/whisper-models/ggml-large-v3.bin",
    "SileroVadModelPath": "/opt/olbrasoft/virtual-assistant/models/silero_vad.onnx"
  }
}
```

### Production Secrets (systemd EnvironmentFile)
```bash
# ~/.config/systemd/user/virtual-assistant.env
AzureTTS__SubscriptionKey=xxxxx
AZURE_SPEECH_REGION=westeurope
GitHub__Token=ghp_xxxxx
GROQ_API_KEY=xxxxx
CEREBRAS_API_KEY=xxxxx
MISTRAL_API_KEY=xxxxx
```

---

## Deployment

### Directory Structure
```
/opt/olbrasoft/virtual-assistant/
├── app/                 # Binaries (dotnet publish output)
│   ├── VirtualAssistant.Service
│   ├── Prompts/         # LLM system prompts
│   └── *.dll
├── config/              # appsettings.json (NO secrets!)
├── data/                # Runtime data
├── models/              # silero_vad.onnx (1.8 MB)
└── icons/               # Tray icons
```

### Commands
```bash
# Build & Test
cd ~/Olbrasoft/VirtualAssistant
dotnet build
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Deploy (ALWAYS use script!)
./deploy/deploy.sh

# Systemd
systemctl --user daemon-reload
systemctl --user restart virtual-assistant

# Verify
curl http://localhost:5055/health
journalctl --user -u virtual-assistant -f
```

---

## Extension Points

### Adding a New TTS Provider

1. Implement `ITtsProvider` interface
2. Register in `TtsServicesExtensions.cs`
3. Add to provider chain configuration

### Adding a New LLM Provider

1. Implement provider in `VirtualAssistant.LlmChain`
2. Add to fallback chain in `LlmServicesExtensions.cs`
3. Configure API key in environment

### Adding a New Voice Command

1. Update `VoiceRouterSystem.md` prompt
2. Add action handler in `ActionExecutorWorker`
3. Implement corresponding service

---

## Related Documentation

- [AGENTS.md](../AGENTS.md) - Machine-readable documentation for AI agents
- [README.md](../README.md) - Project overview and quick start
- [Engineering Handbook](~/GitHub/Olbrasoft/engineering-handbook/) - Development guidelines

---

*Last updated: January 2026*
