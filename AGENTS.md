# VirtualAssistant - Agent Documentation

**Purpose:** Machine-readable project documentation for AI agents
**Last Updated:** 2025-01-26
**Version:** 2.0.0
**Status:** ✅ Stable (all tests passing, production ready)

## Quick Start for Agents

```bash
# 1. Check system health
curl http://localhost:5055/health
systemctl --user status virtual-assistant

# 2. Build & Test
cd ~/Olbrasoft/VirtualAssistant
dotnet build
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# 3. Deploy
./deploy/deploy.sh /opt/olbrasoft/virtual-assistant

# 4. Verify
journalctl --user -u virtual-assistant -n 50
```

## Project Overview

**What:** Linux voice-controlled virtual assistant with inter-agent communication hub
**Why:** Enable voice control of desktop + AI agent orchestration
**How:** .NET 10, PostgreSQL 16+, Whisper STT, TTS provider chain, event-driven workers

### System Hierarchy
```
HUMAN (Architect) ─ makes decisions, approves changes
    ↕
OpenCode (Interactive AI) ─ planning, analysis, task creation
    ↓
VirtualAssistant (Orchestrator) ─ task queue, TTS notifications, Hub API
    ↓ (dispatches tasks)
┌─────────────┬─────────────┬─────────────┐
ClaudeCode   Gemini       Others
(headless)   (future)     (future)
```

**Note:** Push-to-Talk is now a [separate project](https://github.com/Olbrasoft/PushToTalk)

## Essential Paths

```yaml
Source:       ~/Olbrasoft/VirtualAssistant/
Production:   /opt/olbrasoft/virtual-assistant/
  ├─ app/     # Binaries (AppContext.BaseDirectory)
  ├─ config/  # appsettings.json (NO secrets!)
  ├─ data/    # Runtime data, SQLite databases
  ├─ models/  # silero_vad.onnx (1.8 MB)
  └─ icons/   # Tray icons

Whisper:      ~/.local/share/whisper-models/  # FHS-compliant, shared (5.9 GB)
Secrets:      ~/.config/systemd/user/virtual-assistant.env
Systemd:      ~/.config/systemd/user/virtual-assistant.service
Wiki:         https://github.com/Olbrasoft/VirtualAssistant/wiki
```

## Technology Stack

```yaml
Runtime:      .NET 10.0
Database:     PostgreSQL 16+ with pgvector extension
Audio:        PipeWire/PulseAudio, NAudio 2.2.1
STT:          Whisper.NET 1.9.0 (ggml-large-v3 model)
VAD:          Silero VAD ONNX (ONNX Runtime 1.21.0)
TTS:          AzureTTS, EdgeTTS, VoiceRss, Google, Piper (circuit breaker chain)
Embeddings:   Ollama (nomic-embed-text, 768d vectors)
LLM:          Groq, Cerebras, Mistral (rate limit fallback)
ORM:          Entity Framework Core 10.0
Testing:      xUnit, Moq
Desktop:      GTK (tray app)
```

## Architecture Components

### Voice Workers (Event-Driven)
```
AudioCapturerWorker → AudioChunkCapturedEvent
                            ↓
                    VoiceActivityWorker → VoiceActivityDetectedEvent
                                                    ↓
                                        TranscriptionRouterWorker
                                                    ↓
                                            ActionExecutorWorker
```

**1. AudioCapturerWorker**
- Audio capture via PipeWire (pw-record)
- Mute-aware (IManualMuteService)
- RMS calculation for volume
- Tests: `AudioCapturerWorkerTests.cs`

**2. VoiceActivityWorker**
- Silero VAD ONNX inference
- Speech start/end detection
- Tests: `VoiceActivityWorkerTests.cs`

**3. TranscriptionRouterWorker**
- Whisper.NET transcription
- LLM routing (Groq → Cerebras → Mistral)
- Action decision (opencode, respond, ignore, savenote)

**4. ActionExecutorWorker**
- Executes LLM actions
- Calls OpenCode API, TTS, saves notes

### TTS Provider Chain (Circuit Breaker)
```
IVirtualAssistantSpeaker
    ↓
ITtsProviderChain (circuit breaker pattern)
    ↓
AzureTTS → HttpEdgeTts → VoiceRss → Google → Piper
```

- **Primary:** AzureTTS (0.5M chars/month free)
- **Circuit Breaker:** Opens after 3 failures, resets after 300s
- **Credentials:** `~/.config/systemd/user/virtual-assistant.env`

### Core Services

**AgentHubService** (`VirtualAssistant.Core`)
- Inter-agent messaging
- Session management (session_id deduplication)
- Phase tracking (Start=0, Progress=1, Complete=2)

**AgentTaskService** (`VirtualAssistant.Core`)
- Task queue CRUD operations
- Approval workflow
- Task history

**TaskDistributionService** (`VirtualAssistant.Core`)
- Auto-dispatches approved tasks (10s interval)
- Checks agent idle state
- ClaudeCode headless mode: `claude -p "..." --output-format json`

**DependentServicesManager** (`VirtualAssistant.Core`)
- Manages TextToSpeech.Service lifecycle
- Start/stop from tray menu

### GitHub Integration

- Sync issues from repositories
- Generate embeddings via Ollama (nomic-embed-text, 768d)
- Semantic search with pgvector
- Duplicate detection

## Project Structure

```
src/
├── VirtualAssistant.Service/          # ASP.NET Core host (port 5055)
│   ├── Controllers/                   # REST API
│   ├── Workers/                       # 4 voice workers
│   ├── Program.cs                     # Entry, DI, auto-migrations
│   └── appsettings.json               # Dev config
├── VirtualAssistant.Core/             # Business logic
│   ├── Services/
│   │   ├── AgentHubService.cs
│   │   ├── AgentTaskService.cs
│   │   ├── TaskDistributionService.cs
│   │   └── DependentServicesManager.cs
│   └── Events/                        # Event definitions
├── VirtualAssistant.Voice/            # Audio processing
│   ├── Workers/
│   │   ├── AudioCapturerWorker.cs
│   │   ├── VoiceActivityWorker.cs
│   │   ├── TranscriptionRouterWorker.cs
│   │   └── ActionExecutorWorker.cs
│   ├── Services/TtsProviderChain.cs
│   └── Prompts/                       # LLM system prompts
├── VirtualAssistant.Data/             # Entities, DTOs, Enums
├── VirtualAssistant.Data.EntityFrameworkCore/
│   ├── VirtualAssistantDbContext.cs
│   ├── Migrations/
│   └── Configurations/
├── VirtualAssistant.GitHub/           # GitHub API integration
└── VirtualAssistant.Tray/             # GTK tray application

tests/
├── VirtualAssistant.Core.Tests/
├── VirtualAssistant.Voice.Tests/
├── VirtualAssistant.Data.Tests/
└── VirtualAssistant.GitHub.Tests/
```

## API Reference

### Health & System
```
GET  /health                 # Health check
GET  /api/mute               # Get mute state
POST /api/mute               # Set mute state
```

### Agent Hub (Inter-Agent Communication)
```
POST /api/hub/dispatch-task  # Dispatch task to agent
  body: { agent: "claude", github_issue_number: 123 }

POST /api/hub/complete-task  # Mark task completed
  body: { task_id: 42, status: "completed", result: "..." }

GET  /api/hub/active?agent=claude  # Get active tasks
```

### Task Queue
```
POST /api/tasks/create       # Create task (X-Agent-Name header)
  headers: { X-Agent-Name: "opencode" }
  body: { github_issue_number: 123, summary: "...", requires_approval: true }

GET  /api/tasks/idle/{agent} # Check if agent is idle
GET  /api/tasks/pending/{agent}  # Get pending tasks
POST /api/tasks/{id}/approve     # Approve task
POST /api/tasks/{id}/complete    # Complete task
```

### TTS
```
POST /api/tts/speak          # Speak text
  body: { text: "...", source: "opencode" }

POST /api/tts/stop           # Stop current playback
POST /api/tts/flush-queue    # Flush TTS queue
```

### GitHub
```
GET  /api/github/search?q=...&limit=5  # Semantic search
GET  /api/github/duplicates?title=...  # Find duplicates
POST /api/github/sync/{owner}/{repo}   # Sync repository
POST /api/github/embeddings            # Generate embeddings
```

## Database Schema

### Core Tables
```sql
agents                # (id, name, label, is_active)
agent_messages        # (id, source_agent, target_agent, status, phase, session_id)
agent_tasks           # (id, github_issue_number, status, target_agent_id)
github_repositories   # (id, owner, name, full_name)
github_issues         # (id, repository_id, issue_number, title_embedding, body_embedding)
voice_transcriptions  # (id, transcribed_text, source_app, duration_ms)
```

**Embeddings:** `vector(768)` for pgvector semantic search

## Services & Ports

| Service | Port | Command |
|---------|------|---------|
| virtual-assistant | 5055 | `systemctl --user {status\|restart} virtual-assistant` |
| logs-viewer | 5053 | `systemctl --user {status\|restart} virtual-assistant-logs` |
| edge-tts-server | 5555 | `systemctl --user {status\|restart} edge-tts-server` |

**Logs:** `journalctl --user -u virtual-assistant -f`

## Deployment

### Prerequisites
- PostgreSQL 16+ with pgvector extension
- Ollama running (localhost:11434) with nomic-embed-text model
- Whisper model: `~/.local/share/whisper-models/ggml-large-v3.bin`
- Silero VAD: `/opt/olbrasoft/virtual-assistant/models/silero_vad.onnx`

### Production Secrets
File: `~/.config/systemd/user/virtual-assistant.env`
```bash
# Azure TTS (Primary)
AzureTTS__SubscriptionKey=xxxxx
AZURE_SPEECH_REGION=westeurope

# GitHub
GitHub__Token=ghp_xxxxx

# LLM Providers
GROQ_API_KEY=xxxxx
CEREBRAS_API_KEY=xxxxx
MISTRAL_API_KEY=xxxxx

# Database
ConnectionStrings__DefaultConnection=Host=localhost;Database=virtual_assistant;...
```

### Deploy Commands
```bash
# 1. Build & Test
cd ~/Olbrasoft/VirtualAssistant
dotnet build -c Release
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# 2. Deploy (RECOMMENDED - creates structure, copies files)
./deploy/deploy.sh /opt/olbrasoft/virtual-assistant

# 3. Restart service
systemctl --user daemon-reload
systemctl --user restart virtual-assistant

# 4. Verify
systemctl --user status virtual-assistant
curl http://localhost:5055/health
journalctl --user -u virtual-assistant -n 50
```

### Directory Structure After Deploy
```
/opt/olbrasoft/virtual-assistant/
├── app/                 # Binaries (dotnet publish output)
│   ├── VirtualAssistant.Service
│   ├── Prompts/         # LLM system prompts
│   └── *.dll
├── config/              # appsettings.json (NO secrets!)
├── data/                # Runtime data, SQLite DBs
├── models/              # silero_vad.onnx
└── icons/               # Tray icons
```

## Development Workflow for Agents

### 1. Check Documentation
- Read this file (AGENTS.md)
- Check wiki: https://github.com/Olbrasoft/VirtualAssistant/wiki
- Review CLAUDE.md (project-specific rules)

### 2. Verify Current State
```bash
systemctl --user status virtual-assistant
journalctl --user -u virtual-assistant -n 50
curl http://localhost:5055/health
```

### 3. Create Feature Branch
```bash
git checkout -b feature/issue-N-description
```

### 4. Make Changes + Tests
- Write code
- Write xUnit tests (with Moq, NOT NSubstitute)
- Pattern: Arrange-Act-Assert
- Naming: `Method_Scenario_ExpectedResult`

### 5. Test & Deploy
```bash
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
./deploy/deploy.sh /opt/olbrasoft/virtual-assistant
systemctl --user restart virtual-assistant
```

### 6. Commit & Push
```bash
git add .
git commit -m "Implement feature (#N)"
git push -u origin feature/issue-N-description
```

### 7. Create PR
**Do NOT merge without user approval!**

## Testing Standards

- **Framework:** xUnit + Moq (NOT NUnit/NSubstitute)
- **Pattern:** Arrange-Act-Assert
- **Naming:** `Method_Scenario_ExpectedResult`
- **Coverage:** Aim for >80%
- **Structure:** Separate test project per source project
  - `VirtualAssistant.Core` → `VirtualAssistant.Core.Tests`
  - `VirtualAssistant.Voice` → `VirtualAssistant.Voice.Tests`

## Known Issues & Solutions

### "AzureTTS not configured"
**Solution:** Add to `~/.config/systemd/user/virtual-assistant.env`:
```bash
AzureTTS__SubscriptionKey=xxxxx
AZURE_SPEECH_REGION=westeurope
```
Restart: `systemctl --user restart virtual-assistant`

### Whisper model not found
**Solution:** Ensure model in FHS-compliant location:
```bash
ls -lh ~/.local/share/whisper-models/ggml-large-v3.bin
# Should be ~3GB
```

### GitHub search returns nothing
**Solution:** Check Ollama and generate embeddings:
```bash
curl http://localhost:11434/api/tags  # Verify nomic-embed-text
curl -X POST http://localhost:5055/api/github/embeddings  # Regenerate
```

### Audio capture fails
**Solution:** Check PipeWire:
```bash
pw-cli info all | grep -i "audio.*source"
pactl list sources short
```

## Recent Changes (v2.0.0)

### Changed
- Deploy path: `~/virtual-assistant/main/` → `/opt/olbrasoft/virtual-assistant/`
- Workers: `ContinuousListenerWorker` → 4 specialized workers
- TTS: EdgeTTS primary → AzureTTS primary (circuit breaker chain)
- Whisper: App-specific → FHS `/~/.local/share/whisper-models/`
- Config: `ContinuousListener` → `Audio` section
- Secrets: appsettings.json → systemd EnvironmentFile

### Removed
- Push-to-Talk (now separate: https://github.com/Olbrasoft/PushToTalk)

### Added
- DependentServicesManager
- IManualMuteService (mouse button mute)
- TtsProviderChain with circuit breaker
- ClaudeCode headless dispatch
- Ollama embeddings (768d)

## Roadmap

### Current Priorities
1. ✅ Stability & reliability
2. ✅ Comprehensive agent documentation
3. 🔄 Test coverage improvements
4. 🔄 Performance optimization (Whisper inference)

### Planned Features
- [ ] Multi-agent conversation sessions
- [ ] Gemini agent integration
- [ ] Custom plugin system
- [ ] Advanced voice commands
- [ ] Performance monitoring dashboard

## Resources

| Resource | URL |
|----------|-----|
| Repository | https://github.com/Olbrasoft/VirtualAssistant |
| Wiki | https://github.com/Olbrasoft/VirtualAssistant/wiki |
| ClaudeCode | https://github.com/Olbrasoft/ClaudeCode |
| Push-to-Talk | https://github.com/Olbrasoft/PushToTalk |
| Engineering Handbook | ~/GitHub/Olbrasoft/engineering-handbook/ |

---

**Last Updated:** 2025-01-26
**Maintained by:** Human architect + AI agents (OpenCode, Claude)
