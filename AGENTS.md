# AGENTS.md

## DEPLOY

```bash
cd ~/Olbrasoft/VirtualAssistant && ./deploy/deploy.sh
```

Target: `~/apps/virtual-assistant/` (defined in deploy/virtual-assistant.service WorkingDirectory)

Manual (emergency):
```bash
dotnet test && dotnet publish src/VirtualAssistant.Service/VirtualAssistant.Service.csproj -c Release -o ~/apps/virtual-assistant/ --no-self-contained && systemctl --user restart virtual-assistant.service
```

## SERVICES

| Service | Port | Command |
|---------|------|---------|
| virtual-assistant | 5055 | `systemctl --user {status|restart|stop} virtual-assistant.service` |
| logs-viewer | 5053 | `systemctl --user {status|restart} virtual-assistant-logs.service` |

Logs: `journalctl --user -u virtual-assistant.service -f`

## API

| Endpoint | Method | Params |
|----------|--------|--------|
| /api/github/search | GET | q(required), target(Title/Body/Both), limit |
| /api/github/duplicates | GET | title(required), body, threshold(0-1), limit |
| /api/github/issues/open/{owner}/{repo} | GET | - |
| /api/github/sync | POST | - |
| /health | GET | - |

## STRUCTURE

```
src/
  VirtualAssistant.Service/     # ASP.NET Core main
  VirtualAssistant.GitHub/      # GitHub sync+search
  VirtualAssistant.Data/        # Entities
  VirtualAssistant.Data.EntityFrameworkCore/  # EF+Migrations
  VirtualAssistant.PushToTalk/  # Mouse monitors
  VirtualAssistant.Voice/       # TTS/STT
  VirtualAssistant.Core/        # Domain
tests/
  VirtualAssistant.*.Tests/     # xUnit+Moq
deploy/
  deploy.sh                     # Deploy script
```

## DEPS

| Dependency | URL | Purpose |
|------------|-----|---------|
| Ollama | localhost:11434 | Embeddings (nomic-embed-text, 768d) |
| PostgreSQL | localhost | DB (pgvector extension) |

## PUSHTOTALK

Architecture (SOLID):
```
EvdevConstants.cs        # Linux evdev constants
IInputDeviceDiscovery    # Device discovery interface
InputDeviceDiscovery     # /proc/bus/input/devices parser
IMultiClickDetector      # Click detection interface
MultiClickDetector       # Thread-safe timing
IButtonAction            # Strategy pattern interface
  KeyPressAction         # Single key
  KeyComboAction         # 2-key combo
  KeyComboWithTwoModifiersAction  # 3-key combo
  ShellCommandAction     # Shell exec
  NoAction               # Null object
ButtonClickHandler       # Detection+action
BluetoothMouseMonitor    # BT mouse (BluetoothMouse3600)
UsbMouseMonitor          # USB mouse (USB Optical Mouse)
```

Button mappings:
```
BT LEFT:   1x=CapsLock  2x=ESC      3x=Command
BT RIGHT:  1x=None      2x=Ctrl+Shift+V  3x=Ctrl+C
BT MIDDLE: 1x=Enter
USB LEFT:  1x=CapsLock  2x=ESC
USB RIGHT: 1x=None      2x=Ctrl+Shift+V  3x=Ctrl+C
```

Key: EVIOCGRAB grabs device exclusively.

## WORKFLOW

1. `dotnet test` (MUST pass)
2. `./deploy/deploy.sh`
3. Verify: `systemctl --user status virtual-assistant.service`

Migrations auto-apply on startup.

## TROUBLESHOOT

404 errors: Wrong deploy path. Run `./deploy/deploy.sh`
Service fail: `journalctl --user -u virtual-assistant.service -n 50`
Port conflict: `ss -tulpn | grep 5055`
Embeddings fail: `curl localhost:11434/api/tags` then `ollama pull nomic-embed-text`
