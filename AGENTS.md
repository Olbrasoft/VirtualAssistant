# AGENTS.md - VirtualAssistant Project Instructions

This file provides guidance for AI agents (Claude Code, OpenCode) when working with this repository.

## Deployment Configuration

### Critical: Deployment Path

**ALWAYS deploy to this exact path:**

```
~/virtual-assistant/main/
```

**Full path:** `/home/jirka/virtual-assistant/main/`

**NEVER use:**
- `~/virtual-assistant/service/` ❌
- `~/virtual-assistant/` (root) ❌
- Any other subdirectory ❌

### Deployment Process

**ALWAYS use the deploy script. NEVER deploy manually.**

```bash
cd ~/Olbrasoft/VirtualAssistant
./deploy/deploy.sh
```

The deploy script will:
1. Run all tests (MUST pass)
2. Build and publish to `~/virtual-assistant/main/`
3. Install systemd services if needed
4. Restart services automatically
5. Display service status

### Manual Deployment (Emergency Only)

If the deploy script fails and manual deployment is absolutely necessary:

```bash
# 1. Run tests first
cd ~/Olbrasoft/VirtualAssistant
dotnet test

# 2. Build and publish to EXACT path
dotnet publish src/VirtualAssistant.Service/VirtualAssistant.Service.csproj \
  -c Release \
  -o ~/virtual-assistant/main/ \
  --no-self-contained

# 3. Restart service
systemctl --user restart virtual-assistant.service

# 4. Verify
systemctl --user status virtual-assistant.service
```

## Systemd Services

### Main Service

- **Service file:** `~/.config/systemd/user/virtual-assistant.service`
- **Working directory:** `/home/jirka/virtual-assistant/main/`
- **Executable:** `/home/jirka/virtual-assistant/main/VirtualAssistant.Service`

### Log Viewer Service

- **Service file:** `~/.config/systemd/user/virtual-assistant-logs.service`
- **Port:** 5053
- **URL:** http://localhost:5053

### Service Commands

```bash
# Check status
systemctl --user status virtual-assistant.service

# Restart
systemctl --user restart virtual-assistant.service

# View logs
journalctl --user -u virtual-assistant.service -f

# Stop
systemctl --user stop virtual-assistant.service
```

## API Endpoints

After deployment, the following endpoints are available:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/github/search` | GET | Semantic search for GitHub issues |
| `/api/github/duplicates` | GET | Find potential duplicate issues |
| `/api/github/issues/open/{owner}/{repo}` | GET | List open issues |
| `/api/github/sync` | POST | Trigger GitHub sync |
| `/health` | GET | Health check |

### Search API Parameters

**`/api/github/search`:**
- `q` (required): Search query text
- `target`: Search target (`Title`, `Body`, `Both`) - default: `Both`
- `limit`: Max results (default: 10)

**`/api/github/duplicates`:**
- `title` (required): Issue title to check
- `body`: Issue body text
- `threshold`: Similarity threshold 0.0-1.0 (default: 0.7)
- `limit`: Max results (default: 5)

## Project Structure

```
VirtualAssistant/
├── src/
│   ├── VirtualAssistant.Service/          # Main ASP.NET Core service
│   ├── VirtualAssistant.GitHub/           # GitHub sync & search
│   ├── VirtualAssistant.Data/             # Entity models
│   └── VirtualAssistant.Data.EntityFrameworkCore/  # EF Core + Migrations
├── tests/
│   └── VirtualAssistant.GitHub.Tests/     # Unit tests
└── deploy/
    ├── deploy.sh                          # Deployment script
    ├── virtual-assistant.service          # Systemd service
    └── virtual-assistant-logs.service     # Log viewer service
```

## Development Workflow

### Before Any Changes

1. Pull latest changes
2. Ensure all tests pass: `dotnet test`

### After Making Changes

1. Run tests: `dotnet test`
2. Deploy using script: `./deploy/deploy.sh`
3. Verify service is running: `systemctl --user status virtual-assistant.service`
4. Test endpoints if applicable

### Database Migrations

After adding migrations:

```bash
# Apply migrations (service applies on startup)
systemctl --user restart virtual-assistant.service

# Or manually:
cd src/VirtualAssistant.Service
dotnet ef database update --project ../VirtualAssistant.Data.EntityFrameworkCore
```

## External Dependencies

### Ollama (Local Embeddings)

- **URL:** http://localhost:11434
- **Model:** nomic-embed-text (768 dimensions)
- **Required for:** Semantic search, duplicate detection

### PostgreSQL

- **Host:** localhost
- **Database:** virtual_assistant
- **Extensions:** pgvector (for vector similarity search)

## Configuration

Configuration is in `appsettings.json`:

```json
{
  "Embeddings": {
    "Provider": "Ollama",
    "Model": "nomic-embed-text",
    "Dimensions": 768,
    "BaseUrl": "http://localhost:11434"
  }
}
```

## Troubleshooting

### 404 on API Endpoints

**Cause:** Service running old code (wrong deployment path)

**Fix:**
```bash
# Check which executable is running
systemctl --user status virtual-assistant.service

# Verify correct path in service file
cat ~/.config/systemd/user/virtual-assistant.service | grep ExecStart

# Redeploy using script
./deploy/deploy.sh
```

### Service Won't Start

```bash
# Check logs
journalctl --user -u virtual-assistant.service -n 50

# Check if port is in use
ss -tulpn | grep 5055
```

### Embedding Generation Fails

1. Check Ollama is running: `curl http://localhost:11434/api/tags`
2. Verify model exists: `ollama list`
3. Pull model if missing: `ollama pull nomic-embed-text`
