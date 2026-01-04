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

## CI/CD & Automation

### Pull Request Workflow

**Automated Code Review:**
- **GitHub Copilot** automatically reviews ALL pull requests
- Reviews appear as PR comments within minutes of PR creation
- Address ALL review comments before merging
- Common issues flagged: threading, performance, null checks, documentation

**IMPORTANT:** Never merge PR without addressing Copilot's code review comments!

### Automated Deployment

**GitHub Actions workflow** (`.github/workflows/deploy.yml`) triggers automatically:

**Trigger:** Push to `main` branch (after PR merge)

**Runs on:** Self-hosted GitHub Actions runner (`~/actions-runner`)

**Pipeline steps:**
1. ✅ Checkout code (VirtualAssistant + Data dependency)
2. ✅ Restore dependencies (`dotnet restore`)
3. ✅ Build (`dotnet build --configuration Release`)
4. ✅ Run tests (`dotnet test` - must pass!)
5. ✅ Publish to `/opt/olbrasoft/virtual-assistant/app/`
6. ✅ Copy assets (icons, sounds) to deployment directory
7. ✅ Restart `virtual-assistant.service`

**Deployment is FULLY AUTOMATED** - no manual steps required after merge!

**Monitor deployment:**
```bash
# Watch GitHub Actions runner logs
journalctl -u actions.runner.* -f

# Verify deployment success
systemctl --user status virtual-assistant.service
journalctl --user -u virtual-assistant.service -n 50
```

**Deployment fails if:**
- Build fails
- Tests fail
- Secrets missing in `~/.config/systemd/user/virtual-assistant.env`

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
- Whisper models: `~/.local/share/whisper-models/` (5.9 GB, FHS-compliant)
- Secrets: `~/.config/systemd/user/virtual-assistant.env`

## Architecture

Clean Architecture with CQRS pattern:
- **VirtualAssistant.Service** - ASP.NET Core main service (port 5055)
- **VirtualAssistant.Core** - Domain logic, AgentHubService, TaskDistributionService
- **VirtualAssistant.Voice** - TTS/STT with **inline Whisper.net** (GPU-accelerated), VAD (Silero ONNX), LLM routing
- **VirtualAssistant.Data** - Entities, DTOs
- **VirtualAssistant.Data.EntityFrameworkCore** - DbContext, migrations (auto-apply on startup)
- **VirtualAssistant.GitHub** - GitHub API, issue sync with embeddings

**Speech-to-Text (inline):**
- `WhisperSpeechTranscriber` - Direct Whisper.net integration (no gRPC microservice)
- GPU acceleration via CUDA (Whisper.net.Runtime.Cuda.Linux 1.9.0)
- Model caching in VRAM for performance
- Thread-safe concurrent transcription
- Models: ggml-medium.bin (continuous listening), ggml-large-v3-turbo.bin (dictation)

## Dependencies

| Dependency | Location | Purpose |
|------------|----------|---------|
| Whisper models | `~/.local/share/whisper-models/` | Speech-to-text (shared, FHS-compliant) |
| Ollama | localhost:11434 | Embeddings (nomic-embed-text, 768d) |
| PostgreSQL | localhost | DB with pgvector extension |
| EdgeTTS Server | localhost:5555 | Fallback TTS provider |

## Desktop Context Awareness (LinuxDesktop Integration)

VirtualAssistant monitors your desktop activity via LinuxDesktop NuGet packages to provide context-aware assistance.

### Features

**✅ Context-aware LLM prompts**
- Programming prompt when in IDE (code, cursor, pycharm, rider)
- Chat prompt when in messaging apps (whatsapp-for-linux, telegram, slack)
- Search prompt when browsing (chrome, firefox, edge)
- General prompt as fallback

**✅ Intelligent notifications**
- Skips "Claude Code finished task" if you're already in Claude Code
- Delivers notifications only when you're in different app
- Urgent notifications always delivered (contains "urgent", "critical", "error")
- Always delivers SystemAlert and UserMessage sources

### Requirements

**GNOME Shell Extensions (OPTIONAL, recommended for full functionality):**
1. `window-calls@domandoman.xyz` - Window/workspace D-Bus API
2. `focus-tracker@olbrasoft.cz` - Custom extension for focus events

**Verify extensions:**
```bash
gnome-extensions list --enabled | grep -E "window-calls|focus-tracker"
```

**Install missing extensions:**
```bash
gnome-extensions enable window-calls@domandoman.xyz
gnome-extensions enable focus-tracker@olbrasoft.cz
```

### Configuration

**appsettings.json:**
```json
{
  "DesktopMonitoring": {
    "Enabled": true,
    "PollingIntervalMs": 500,
    "GracefulDegradation": true,
    "LogContextChanges": true
  },
  "ContextMapping": {
    "Programming": ["code", "cursor", "rider", "pycharm", "idea"],
    "Chat": ["whatsapp-for-linux", "telegram", "slack", "discord"],
    "Browsing": ["chrome", "firefox", "chromium", "brave", "edge"]
  },
  "NotificationFiltering": {
    "Enabled": true,
    "AppNameMapping": {
      "Claude Code": "code",
      "OpenCode": "code",
      "VS Code": "code",
      "GitHub": "chrome",
      "PyCharm": "pycharm"
    },
    "AlwaysDeliverSources": ["SystemAlert", "UserMessage"]
  }
}
```

### Troubleshooting

#### Extension Not Running

**Symptoms:** Logs show "Desktop monitoring unavailable"

**Solution:**
```bash
# Check extensions
gnome-extensions list --enabled

# Enable missing extensions
gnome-extensions enable window-calls@domandoman.xyz
gnome-extensions enable focus-tracker@olbrasoft.cz

# Restart GNOME Shell (X11)
# Press Alt+F2, type 'r', press Enter

# Or logout/login (Wayland)
```

#### Context Changes Not Detected

**Symptoms:** Same prompt used for all apps

**Solution:**
```bash
# Check logs for context detection
journalctl --user -u virtual-assistant.service -f | grep "context"

# Verify GNOME extensions running
dbus-send --session --print-reply \
  --dest=org.gnome.Shell \
  /org/gnome/Shell \
  org.gnome.Shell.Eval \
  string:'global.get_window_actors().length'

# Should return number of windows
```

#### Graceful Degradation

If GNOME extensions are not available:
- Service starts without errors
- Desktop monitoring disabled
- All notifications delivered (no filtering)
- General prompt used for all contexts
- Warnings logged but no crashes

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

## Desktop Monitor Web UI

**Live dashboard** for monitoring desktop context changes in real-time.

**Access:** `http://localhost:5055/desktop-monitor/`

**Features:**
- ✅ Real-time workspace/window/app tracking via SignalR
- ✅ VS Code-like dark theme
- ✅ Color-coded event log (workspace changes, focus changes)
- ✅ Auto-reconnect on connection loss
- ✅ Scrollable log with 500-entry limit

**Technology:**
- SignalR Hub: `/hub/desktop-monitor`
- CDN: SignalR client 8.0.7 (jsdelivr)
- Static files: served from `wwwroot/desktop-monitor/`

**Events broadcasted:**
- `WorkspaceChanged(newIndex, totalWorkspaces)` - Workspace switch
- `FocusChanged(windowTitle, appId, wmClass)` - Window/app focus
- `LogMessage(message)` - Detailed change log

**Architecture:**
- `DesktopMonitorHub` - SignalR hub endpoint
- `DesktopMonitorBroadcastWorker` - Background worker subscribing to `DesktopContextService.ContextChanges`
- `index.html` - Client-side dashboard

## Key API Endpoints

- `/api/github/search?q=...` - Semantic issue search
- `/api/hub/send` - Inter-agent messaging
- `/api/tasks/create` - Task queue (X-Agent-Name header)
- `/api/tts/speak` - Text-to-speech
- `/health` - Health check
- `/hub/desktop-monitor` - Desktop Monitor SignalR hub

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
- **Pull Request workflow:**
  1. Create feature branch from `main`
  2. Push changes and create PR
  3. Wait for automated Copilot code review
  4. Address ALL review comments
  5. Merge to `main` (triggers automatic deployment)

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

## Migration Notes

### SpeechToText Microservice → Inline Whisper.net (2025-12-31)

**What changed:**
- Removed external SpeechToText gRPC microservice
- Integrated Whisper.net directly into VirtualAssistant.Voice
- No more port 5052 or separate `speech-to-text.service`

**Benefits:**
- Lower latency (removed gRPC overhead)
- Simpler deployment (one service instead of two)
- Lower memory usage (single process)
- Easier debugging (all logs in one place)

**Migration completed:**
- ✅ `WhisperSpeechTranscriber` created with model caching
- ✅ DI registration updated
- ✅ `speech-to-text.service` stopped and removed
- ✅ All tests passing
- ✅ Dictation works (617ms transcription time)

**For more details:** See issue #457
