# VirtualAssistant

Linux voice assistant pro ovládání desktopu a integraci s AI coding agenty.

## Funkce

- **Voice-to-OpenCode** – hlasové příkazy směrované do AI coding agenta
- **Push-to-Talk diktování** – přepis hlasu do aktivního okna
- **Kontinuální poslech** – VAD (Voice Activity Detection) s Silero ONNX
- **Multi-provider LLM routing** – Groq, Cerebras, Mistral s automatickým fallbackem
- **Lokální ASR** – WhisperNet s large-v3 modelem

## Architektura

```
┌─────────────────────────────────────────────────────────────────┐
│                        VirtualAssistant                         │
├─────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Voice          │  VirtualAssistant.PushToTalk │
│  - Kontinuální poslech           │  - PTT diktování             │
│  - VAD (Silero ONNX)             │  - Whisper ASR               │
│  - LLM routing                   │  - Text typing (xdotool)     │
├─────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Core           │  VirtualAssistant.Service    │
│  - Rozhraní a abstrakce          │  - Systemd worker            │
│  - Konfigurace                   │  - Tray ikona                │
├─────────────────────────────────────────────────────────────────┤
│  VirtualAssistant.Api            │  VirtualAssistant.Tray       │
│  - REST API                      │  - GTK tray aplikace         │
└─────────────────────────────────────────────────────────────────┘
```

## Projekty

| Projekt | Popis |
|---------|-------|
| `VirtualAssistant.Core` | Sdílené rozhraní, enumy, konfigurace |
| `VirtualAssistant.Voice` | Kontinuální poslech, VAD, LLM routing |
| `VirtualAssistant.PushToTalk` | Push-to-Talk knihovna |
| `VirtualAssistant.PushToTalk.Service` | PTT služba (port 5050) |
| `VirtualAssistant.Service` | Hlavní služba s tray ikonou |
| `VirtualAssistant.Api` | REST API endpoint |
| `VirtualAssistant.Tray` | Standalone tray aplikace |
| `VirtualAssistant.Agent` | AI agent (WIP) |
| `VirtualAssistant.Desktop` | Desktop integrace (WIP) |

## Požadavky

- .NET 10
- Linux (testováno na Debian 13)
- ALSA nebo PulseAudio
- xdotool / dotool (pro text input)
- Whisper model (`ggml-large-v3.bin`)

## Instalace

```bash
# Klonování
git clone https://github.com/Olbrasoft/VirtualAssistant.git
cd VirtualAssistant

# Build
dotnet build

# Testy
dotnet test
```

## Konfigurace

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
# Deploy Push-to-Talk služby
dotnet publish src/VirtualAssistant.PushToTalk.Service/VirtualAssistant.PushToTalk.Service.csproj \
  -c Release -o ~/virtual-assistant/push-to-talk --no-self-contained

# Systemd služba
systemctl --user enable push-to-talk-dictation.service
systemctl --user start push-to-talk-dictation.service
```

## Testování

```bash
# Všechny testy
dotnet test

# Konkrétní projekt
dotnet test tests/VirtualAssistant.Voice.Tests
```

**Aktuální stav:** 90 testů, všechny procházejí.

## LLM Providers

Voice router podporuje více LLM providerů s automatickým fallbackem při rate limitech:

1. **Groq** (primární) – nejrychlejší, `llama-3.3-70b-versatile`
2. **Cerebras** (fallback) – `llama-3.3-70b`
3. **Mistral** (fallback) – `mistral-large-latest`

## Licence

MIT License – viz [LICENSE](LICENSE)
