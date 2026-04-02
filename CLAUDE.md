# VirtualAssistant - Claude Code Guide

Linux voice-controlled virtual assistant with desktop context awareness.

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

## Recent Changes (2025-01-05)

**Project cleanup completed:**
- ✅ Removed `VirtualAssistant.Agent` and `VirtualAssistant.Agent.Tests` (empty placeholder projects)
- ✅ Removed empty query/command directories (GitHubIssueQueries, LlmCorrectionQueries, NotificationQueries)
- ✅ Removed 22 `.gitkeep` placeholder files
- ✅ Removed Agent Hub and Task Queue features (migrations dropped tables)

**CQRS refactoring completed (issues #558-#562):**
- ✅ All queries and commands converted to C# records with primary constructors
- ✅ Removed verbose class-based commands/queries
- ✅ Cleaner, more concise CQRS definitions

**Current project count:** 9 projects (down from 11)

**Database schema changes:**
- Embeddings: OpenAI 1536d → Ollama nomic-embed-text 768d
- New tables: `whisper_transcriptions`, `notifications`, `notification_statuses`, `providers`, `notification_tts_attempts`, `transcription_corrections`, `transcription_correction_usages`
- Dropped tables: `agent_messages`, `agent_tasks`, `agent_task_sends`, `github_issue_agents`

## CI/CD & Automation

### Pull Request Workflow

**Automated Code Review:**
- **GitHub Copilot** automatically reviews ALL pull requests
- Reviews appear as PR comments within minutes of PR creation
- **MUST address ALL review comments before merging**
- Common issues flagged: threading, performance, null checks, documentation
- **NOTE:** Copilot performs only ONE review per PR. No second review after fixes - just verify your fixes address all comments and merge.

**PR Review Process (MANDATORY):**
1. ✅ Create PR and push feature branch
2. ⏳ **Wait for GitHub Copilot code review** (usually within 5 minutes)
3. 📝 **Read ALL review comments carefully**
4. 🔧 **Fix ALL issues** mentioned in review comments
5. ✅ Push fixes to feature branch
6. ⚠️ **VERIFY all comments are fixed** - Use `mcp__github__pull_request_read` with `get_reviews` AND `get_review_comments` to check
7. ✅ **Only then** merge PR to main

**CRITICAL:** Never merge PR without addressing Copilot's code review comments!
**CRITICAL:** Always check BOTH `get_reviews` AND `get_review_comments` before merging - they return different data!

**PR Scope Rule (MANDATORY):**
- **Group of issues** (1 or more) done together → ONE pull request ✅
- **Once PR is created and pushed**, code review starts automatically
- **After PR creation**: ONLY commits for review fixes allowed
- ❌ **NEVER add NEW issues to existing PR** after code review started
- ✅ **New issue(s)** (not in original group) → **NEW branch + NEW PR**

Example (CORRECT):
- Issues #582 + #583 together → Branch `feature/issues-582-583` → PR #591 ✅
- Review fixes for PR #591 → Commit to PR #591 ✅

Example (WRONG):
- Issue #582 → PR #591 created
- Issue #583 (new issue after PR created) → Commit to PR #591 ❌
- Should be: Issue #583 → NEW PR #592 ✅

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
8. ✅ Health check (verifies service responds on `/health`)
9. ✅ Write deploy event to `~/.config/claude-channels/deploy-events/` — triggers asyncRewake hook in Claude Code

**Deployment is FULLY AUTOMATED** - no manual steps required after merge!

### Post-Deploy Verification (Hook-Based Push)

**GitHub Actions writes deploy result to `~/.config/claude-channels/deploy-events/`. A `SessionStart` hook with `asyncRewake: true` watches the directory and automatically wakes Claude Code when a deploy event arrives.** No polling, no flags, no Channels needed.

**How it works:**
1. GitHub Actions deploys → writes `Olbrasoft-VirtualAssistant.json` to deploy-events directory
2. `asyncRewake` hook (inotifywait) detects the file → exits with code 2 → wakes Claude Code
3. `UserPromptSubmit` hook reads the file and injects deploy status into context
4. Claude Code sees `<deploy-complete>` tag and reacts:
   - Verify service is running: `systemctl --user status virtual-assistant.service`
   - Verify new content deployed (e.g., `grep` for changes in `/opt/olbrasoft/virtual-assistant/app/`)
   - Check logs for errors: `journalctl --user -u virtual-assistant.service --since "2 min ago"`
   - Send notification via `mcp__notify__notify` with deployment result
5. Hook deletes the file so it's only shown once

**Configuration:**
- Hook: `~/.claude/hooks/watch-deploy-events.sh` (asyncRewake, watches for new files)
- Hook: `~/.claude/hooks/check-deploy-status.sh` (UserPromptSubmit, reads and injects)
- Events dir: `~/.config/claude-channels/deploy-events/`
- Deploy.yml writes the event file after health check

**If Claude Code is not running during deploy:** the event file persists until next session starts and user sends a prompt.

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
- SecureStore keyfile missing at `~/.config/virtual-assistant/keys/secrets.key`
- Required secrets missing in SecureStore vault

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
- Secrets: `~/.config/virtual-assistant/secrets/secrets.json` (SecureStore encrypted vault)
- Keyfile: `~/.config/virtual-assistant/keys/secrets.key` (chmod 600)

## Architecture

Clean Architecture with CQRS pattern:
- **VirtualAssistant.Service** - ASP.NET Core main service (port 5055)
- **VirtualAssistant.Core** - Domain logic, business services orchestrating queries/commands
- **VirtualAssistant.Voice** - TTS/STT with **inline Whisper.net** (GPU-accelerated), VAD (Silero ONNX), LLM routing
- **VirtualAssistant.Data** - Entities, DTOs, Queries, Commands
- **VirtualAssistant.Data.EntityFrameworkCore** - DbContext, QueryHandlers, CommandHandlers, migrations (auto-apply on startup)
- **VirtualAssistant.GitHub** - GitHub API, issue sync with embeddings

**CQRS Architecture:**
The architecture is CQRS. All queries and commands are in the **VirtualAssistant.Data** project.
Query handler and command handler implementations are in the **VirtualAssistant.Data.EntityFrameworkCore** project.

**Pattern Details:**
- **Queries:** Query definitions (IQuery<TResult>) in VirtualAssistant.Data/Queries/
- **Commands:** Command definitions (ICommand<TResult>) in VirtualAssistant.Data/Commands/
- **QueryHandlers:** Query handler implementations in VirtualAssistant.Data.EntityFrameworkCore/QueryHandlers/
- **CommandHandlers:** Command handler implementations in VirtualAssistant.Data.EntityFrameworkCore/CommandHandlers/
- **Infrastructure:** Olbrasoft.Data.Cqrs.Common (IQueryProcessor, ICommandExecutor - custom mediator pattern)
- **Dependency Injection:** Services depend on IQueryProcessor/ICommandExecutor interfaces, implementations registered in Service layer

**Services Layer (VirtualAssistant.Core):**
- **NEVER use Repository pattern** - Services inject **IQueryProcessor** and **ICommandExecutor** ONLY
- Business logic orchestration - combine data from multiple sources:
  - Database (via CQRS queries/commands)
  - External APIs
  - Domain models
- Services coordinate business operations, NOT data access
- Example: `NotificationService` uses `IQueryProcessor` to get notifications, `ICommandExecutor` to create them

**Testing Strategy:**
- **Unit tests** for QueryHandlers/CommandHandlers: Use **in-memory database** (fast, isolated)
- **Integration tests** with real PostgreSQL: Mark with `[SkipOnCIFact]` (skipped in CI/CD)

**Speech-to-Text (inline):**
- `WhisperSpeechTranscriber` - Direct Whisper.net integration (no gRPC microservice)
- GPU acceleration via CUDA (Whisper.net.Runtime.Cuda.Linux 1.9.0)
- Model caching in VRAM for performance
- Thread-safe concurrent transcription
- Models: ggml-medium.bin (continuous listening), ggml-large-v3-turbo.bin (dictation)

**Google Speech-to-Text:**
- `GoogleSpeechTranscriber` - Google Chromium Speech API v2 endpoint
- Used as primary provider with Whisper as fallback
- Configuration in `appsettings.json` section `GoogleSpeechToText`

**STT Provider Configuration:**
```json
{
  "SpeechProvider": {
    "PrimaryProvider": "google",    // or "whisper"
    "FallbackProvider": "whisper",
    "EnableFallback": true
  },
  "GoogleSpeechToText": {
    "ApiKey": "",                   // Store in SecureStore!
    "Language": "cs-CZ",
    "TimeoutMs": 30000,
    "Enabled": true
  }
}
```

**Adding Google STT API Key to SecureStore:**
```bash
SecureStore set -s ~/.config/virtual-assistant/secrets/secrets.json \
  -k ~/.config/virtual-assistant/keys/secrets.key \
  "GoogleSpeechToText:ApiKey=YOUR_API_KEY"
```

## Agent Support

VirtualAssistant supports multiple AI agents with agent-specific voice differentiation via TTS profiles.

### Supported Agents

| Agent | Voice | Provider | Agent ID | TTS Profile |
|-------|-------|----------|----------|-------------|
| claude-code | Antonín (male) | Azure | 4 | claude-code |
| opencode | Antonín (male) | Azure | 1 | (default) |
| gemini | Vlasta (female) | Azure | 11 | gemini |

### Agent Identification

Agents are identified by `source` parameter in notification API:

```json
POST /api/notifications
{
  "text": "Message to speak",
  "source": "gemini",  // Determines agent and voice
  "issueIds": [123]
}
```

### AgentType Enum

Agents are validated using `AgentType` enum (VirtualAssistant.Data/Enums/AgentType.cs):
- `AgentType.OpenCode = 1`
- `AgentType.ClaudeCode = 4`
- `AgentType.Gemini = 11`

Invalid agent names will throw `ArgumentException` in `NotificationService.MapAgentNameToType()`.

### Voice Selection Mechanism

1. Agent name passed to `VirtualAssistantSpeaker.SpeakAsync(text, agentName)`
2. `TtsService` maps agent name to TTS profile (appsettings.json → TtsProfiles.Profiles)
3. Profile selects voice (e.g., "gemini" → cs-CZ-VlastaNeural, "claude-code" → cs-CZ-AntoninNeural)
4. TTS provider chain attempts synthesis (Azure → EdgeTTS → VoiceRSS → Google → Piper)

### MCP Servers for Agent Integration

Each agent can send notifications via dedicated MCP (Model Context Protocol) servers:

| Agent | MCP Server | Location | Purpose |
|-------|------------|----------|---------|
| claude-code | mcp-notify | ~/apps/mcp-notify/ | Notifications from Claude Code |
| gemini | mcp-notify-gemini | ~/apps/mcp-notify-gemini/ | Notifications from Gemini CLI |

**Configuration** (in `~/.claude.json`):
```json
{
  "mcpServers": {
    "notify": {
      "type": "stdio",
      "command": "node",
      "args": ["/home/jirka/apps/mcp-notify/dist/index.js"],
      "env": {}
    },
    "mcp-notify-gemini": {
      "type": "stdio",
      "command": "node",
      "args": ["/home/jirka/apps/mcp-notify-gemini/dist/index.js"],
      "env": {}
    }
  }
}
```

**Tool Usage** (for agents):
```javascript
// From Claude Code or Gemini CLI
mcp__notify__notify({
  text: "Zahajuji práci na issue #255",
  issueIds: [255]
})

// Or for Gemini specifically
mcp__mcp-notify-gemini__notify({
  text: "Dokončil jsem úkol",
  issueIds: [100]
})
```

**How it works:**
1. Agent calls MCP tool `notify({ text, issueIds })`
2. MCP server sends POST request to `/api/notifications`
3. Notification created in database with correct agent ID
4. TTS speaks notification with agent-specific voice
5. Notification displayed in UI and tracked in database

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

- `/api/github/search?q=...` - Semantic issue search (Ollama nomic-embed-text embeddings)
- `/api/notifications` - Create notifications with TTS (agent-specific voices via `source` parameter)
- `/api/tts/speak` - Text-to-speech (source: claude-code/opencode/gemini/assistant)
- `/api/assistant-speech/start|end` - Echo cancellation control
- `/api/mute` - Mute control (GET/POST)
- `/health` - Health check
- `/hub/desktop-monitor` - Desktop Monitor SignalR hub

## Configuration

### Secrets Management (SecureStore)

All secrets are stored in **SecureStore** encrypted vault:
- **Vault:** `~/.config/virtual-assistant/secrets/secrets.json` (encrypted, safe to backup)
- **Keyfile:** `~/.config/virtual-assistant/keys/secrets.key` (chmod 600, NEVER commit to Git)

**Managing Secrets:**
```bash
# Define paths
SECRETS_PATH=~/.config/virtual-assistant/secrets/secrets.json
KEY_PATH=~/.config/virtual-assistant/keys/secrets.key

# List all secrets
SecureStore get -s $SECRETS_PATH -k $KEY_PATH --all

# Add/update a secret
SecureStore set -s $SECRETS_PATH -k $KEY_PATH "Database:Password=MyPassword"

# Get specific secret
SecureStore get -s $SECRETS_PATH -k $KEY_PATH "Database:Password"

# After changes, restart service
systemctl --user restart virtual-assistant.service
```

**Current Secrets:**
| Key | Description |
|-----|-------------|
| `Database:Password` | PostgreSQL password |
| `GitHub:Token` | GitHub API token |
| `TTS:AzureTTS:SubscriptionKey` | Azure Speech Service key |
| `TTS:VoiceRSS:ApiKey` | VoiceRSS API key |
| `GoogleTTS:ApiKey1`, `GoogleTTS:ApiKey2`, `GoogleTTS:ApiKey3` | Google Cloud TTS keys |
| `GoogleSpeechToText:ApiKey` | Google Speech-to-Text API key |
| `LlmChain:Mistral:ApiKey` | Mistral AI key |
| `LlmChain:Cerebras:ApiKeys` | Cerebras keys (comma-separated) |
| `LlmChain:Groq:ApiKeys` | Groq keys (comma-separated) |

**Setup (new installation):**
```bash
# 1. Install SecureStore CLI
dotnet tool install --global SecureStore.Client

# 2. Create directories
mkdir -p ~/.config/virtual-assistant/secrets
mkdir -p ~/.config/virtual-assistant/keys

# 3. Create vault
SecureStore create -s ~/.config/virtual-assistant/secrets/secrets.json \
  -k ~/.config/virtual-assistant/keys/secrets.key

# 4. Secure keyfile (CRITICAL!)
chmod 600 ~/.config/virtual-assistant/keys/secrets.key

# 5. Add secrets (see table above)
SecureStore set -s $SECRETS_PATH -k $KEY_PATH "Database:Password=xxx"
# ... repeat for all secrets
```

**Full documentation:** See `engineering-handbook/development-guidelines/secrets-management.md`

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

### Technology Constraints

**PROHIBITED Technologies:**
- ❌ **Python** - NEVER use Python scripts, wrappers, or helpers
- All functionality MUST be implemented in **C#/.NET**
- D-Bus communication via **LinuxDesktop** library (GNOME Shell extension returns cursor position)
- UI overlays via **GirCore** (C# GTK4 bindings) or other .NET solutions

### NuGet Package Versioning

**CRITICAL:** All `Olbrasoft.*` packages MUST use wildcard versioning!

```xml
<!-- ✅ CORRECT - Always get latest version -->
<PackageReference Include="Olbrasoft.TextToSpeech.Providers.GoogleCloud" Version="1.*" />
<PackageReference Include="Olbrasoft.Data.Cqrs.Common" Version="1.*" />

<!-- ❌ WRONG - Fixed version gets outdated -->
<PackageReference Include="Olbrasoft.TextToSpeech.Orchestration" Version="1.1.14" />
```

**Why:**
- Olbrasoft packages are published automatically via GitHub Actions on push to main
- New features and fixes are immediately available
- Fixed versions require manual updates across all dependent projects
- Wildcard `1.*` ensures automatic updates within major version

**Exception:** Only use fixed version if specific version compatibility is required (rare).

- **Pull Request workflow:**
  1. Create feature branch from `main`
  2. Push changes and create PR
  3. **Wait for automated Copilot code review** (usually within 5 minutes)
  4. **Read ALL review comments carefully**
  5. **Fix ALL issues** mentioned in review comments
  6. Push fixes to feature branch
  7. **Only then** merge to `main` (triggers automatic deployment)

### Integration Tests & CI/CD

**CRITICAL:** Integration tests MUST NOT run on GitHub Actions!

**Why:**
- Integration tests call external services (LLM APIs, databases, network services)
- CI environment lacks required credentials/secrets
- Tests are timing-sensitive and flaky in loaded CI environment
- May incur API costs or rate limits

**How to mark integration tests:**

1. **Add package** to test project:
   ```xml
   <PackageReference Include="Olbrasoft.Testing.Xunit.Attributes" Version="1.*" />
   ```

2. **Use `[SkipOnCIFact]` instead of `[Fact]`:**
   ```csharp
   using Olbrasoft.Testing.Xunit.Attributes;

   [SkipOnCIFact]  // Skips in CI, runs locally
   public async Task LlmRouter_CallsRealAPI_ReturnsResponse()
   {
       // Test that calls external LLM API
   }
   ```

3. **Use for:**
   - Tests calling external APIs (LLM providers, GitHub API, etc.)
   - Tests with timing dependencies (`Task.Delay`, SignalR broadcasts)
   - Tests requiring specific system state (GNOME extensions, D-Bus)
   - Database integration tests requiring PostgreSQL

**Verification:**
```bash
# Runs locally
dotnet test

# Skips in CI environment
CI=true dotnet test  # Test should be skipped
```

**Examples:**
- `VirtualAssistant.LlmChain.IntegrationTests` - All tests marked with `[SkipOnCIFact]`
- `VirtualAssistant.Service.Tests/Workers/DesktopMonitorBroadcastWorkerTests.cs` - Timing-sensitive SignalR test

**Filter in CI pipeline:**
```bash
# GitHub Actions uses this filter
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
```

This ensures fast, reliable CI builds without external dependencies.

## Deployment Checklist

**BEFORE claiming "deployment completed":**

1. ✅ Binaries deployed to `/opt/olbrasoft/virtual-assistant/app/`
2. ✅ Config in `/opt/olbrasoft/virtual-assistant/config/appsettings.json`
3. ⚠️ **SecureStore keyfile at `~/.config/virtual-assistant/keys/secrets.key`** (chmod 600)
4. ⚠️ **All required secrets in SecureStore vault**
5. ✅ Service restarted and running
6. ✅ **LOGS checked - NO "not configured" errors**

**Verify secrets:**
```bash
# Check keyfile exists and has correct permissions
ls -la ~/.config/virtual-assistant/keys/secrets.key
# Should show: -rw------- (600)

# Check all secrets are present
SecureStore get -s ~/.config/virtual-assistant/secrets/secrets.json \
  -k ~/.config/virtual-assistant/keys/secrets.key --all

# Check logs for errors
journalctl --user -u virtual-assistant.service -n 100 | grep -i "not configured\|not found"
# Should return NOTHING!
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| 404 errors | Wrong deploy path - check service is running from `/opt/olbrasoft/virtual-assistant/app/` |
| Service fail | `journalctl --user -u virtual-assistant.service -n 50` |
| Port conflict | `ss -tulpn \| grep 5055` |
| Embeddings fail | `curl localhost:11434/api/tags` then `ollama pull nomic-embed-text` |
| Azure TTS fail | Check `TTS:AzureTTS:SubscriptionKey` exists in SecureStore |
| "not configured" | Missing secrets in SecureStore - see Secrets Management section |

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
