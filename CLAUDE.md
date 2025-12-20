# VirtualAssistant - Claude Code Guide

Linux voice-controlled virtual assistant with inter-agent communication hub.

## Build & Deploy

```bash
# Build
cd ~/Olbrasoft/VirtualAssistant && dotnet build

# Test (MUST pass before deployment)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Deploy to production
cd ~/Olbrasoft/VirtualAssistant && ./deploy/deploy.sh /opt/olbrasoft/virtual-assistant
```

**Production path:** `/opt/olbrasoft/virtual-assistant/` (ONLY deployment target)

## Directory Structure

```
/opt/olbrasoft/virtual-assistant/
├── app/                          # Binaries (AppContext.BaseDirectory)
│   ├── VirtualAssistant.Service
│   ├── Prompts/                  # LLM prompts
│   └── *.dll
├── config/                       # Configuration (no secrets!)
│   └── appsettings.json
├── data/                         # Runtime data (DB, cache)
│   └── notifications.db
├── icons/                        # Tray icons
├── models/                       # App-specific models ONLY
│   └── silero_vad.onnx          # 1.8 MB VAD model
└── certs/                        # TLS certificates
```

**Shared resources:**
- Whisper models: `~/.local/share/whisper-models/` (5.9 GB, shared with PushToTalk)
- Secrets: `~/.config/systemd/user/virtual-assistant.env`

## Architecture

Clean Architecture with CQRS pattern:
- **VirtualAssistant.Service** - ASP.NET Core main service (port 5055)
- **VirtualAssistant.Core** - Domain logic, AgentHubService, TaskDistributionService
- **VirtualAssistant.Voice** - TTS/STT, VAD (Silero ONNX), LLM routing
- **VirtualAssistant.Data** - Entities, DTOs
- **VirtualAssistant.Data.EntityFrameworkCore** - DbContext, migrations (auto-apply on startup)
- **VirtualAssistant.GitHub** - GitHub API, issue sync with embeddings

## Dependencies

| Dependency | Location | Purpose |
|------------|----------|---------|
| Whisper models | `~/.local/share/whisper-models/` | Speech-to-text (shared, FHS-compliant) |
| Ollama | localhost:11434 | Embeddings (nomic-embed-text, 768d) |
| PostgreSQL | localhost | DB with pgvector extension |
| EdgeTTS Server | localhost:5555 | Fallback TTS provider |

## Services

| Service | Port | Command |
|---------|------|---------|
| virtual-assistant | 5055 | `systemctl --user {status|restart|stop} virtual-assistant.service` |
| logs-viewer | 5053 | `systemctl --user {status|restart} virtual-assistant-logs.service` |
| edge-tts-server | 5555 | `systemctl --user {status|restart} edge-tts-server.service` |

**Logs:**
```bash
journalctl --user -u virtual-assistant.service -f
```

## Key API Endpoints

- `/api/github/search?q=...` - Semantic issue search
- `/api/hub/send` - Inter-agent messaging
- `/api/tasks/create` - Task queue (X-Agent-Name header)
- `/api/tts/speak` - Text-to-speech
- `/health` - Health check

## Configuration

### Secrets Management

**Production secrets** are in systemd EnvironmentFile:

**~/.config/systemd/user/virtual-assistant.env:**
```bash
# Azure TTS (Priority 1 provider)
AzureTTS__SubscriptionKey=xxxxx
AZURE_SPEECH_REGION=westeurope

# GitHub
GitHub__Token=ghp_xxxxx

# Database
ConnectionStrings__DefaultConnection=Host=localhost;Database=virtual_assistant;...
```

**Source:** `~/Dokumenty/přístupy/api-keys.md`

### TTS Provider Chain

Priority order (circuit breaker pattern):
1. **AzureTTS** - Azure Speech Service (0.5M chars/month free)
2. **EdgeTTS-WebSocket** - Edge TTS server (localhost:5555, fallback)
3. **VoiceRSS** - VoiceRSS API (key from file)
4. **GoogleTTS** - Google TTS
5. **Piper** - Local offline TTS

## Development Standards

- **.NET 10** (`net10.0`) for all projects
- **xUnit + Moq** for testing (NOT NUnit/NSubstitute)
- **Sub-issues** for task steps (NOT markdown checkboxes)
- **Push frequently** after every significant change
- **Never close issues** without user approval

## Deployment Checklist

**BEFORE claiming "deployment completed":**

1. ✅ Binaries deployed to `/opt/olbrasoft/virtual-assistant/app/`
2. ✅ Config in `/opt/olbrasoft/virtual-assistant/config/appsettings.json`
3. ⚠️ **SECRETS in `~/.config/systemd/user/virtual-assistant.env`**
4. ✅ systemd service has `EnvironmentFile=...` directive
5. ✅ Service restarted and running
6. ✅ **LOGS checked - NO "not configured" errors**

**Verify secrets:**
```bash
journalctl --user -u virtual-assistant.service -n 100 | grep -i "not configured\|not available"
# Should return NOTHING!
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| 404 errors | Wrong deploy path - check service is running from `/opt/olbrasoft/virtual-assistant/app/` |
| Service fail | `journalctl --user -u virtual-assistant.service -n 50` |
| Port conflict | `ss -tulpn \| grep 5055` |
| Embeddings fail | `curl localhost:11434/api/tags` then `ollama pull nomic-embed-text` |
| Azure TTS fail | Check `~/.config/systemd/user/virtual-assistant.env` has `AzureTTS__SubscriptionKey` |
| "not configured" | Missing secrets - see Secrets Management section |

## Known Issues

See `MISTAKES.md` for lessons learned from past deployment mistakes.
