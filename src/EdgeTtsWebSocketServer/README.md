# Edge TTS WebSocket Server

Lightweight HTTP API server pro text-to-speech komunikaci přímo s Microsoft Edge TTS službou přes WebSocket.

## Funkce

- 🚀 **Přímá WebSocket komunikace** s Microsoft Edge TTS API
- 🔥 **HTTP REST API** pro snadné volání z bash/curl
- 💾 **Cache systém** pro rychlejší přehrávání často používaných frází
- 🔒 **Lock mechanismus** pro synchronizaci s mikrofonem
- 🇨🇿 **Podpora českých hlasů** (AntoninNeural - mužský, VlastaNeural - ženský)
- ⚡ **Rychlé** - běží jako systemd služba na pozadí

## Architektura

```
┌─────────────┐          ┌──────────────────┐          ┌─────────────────┐
│   Bash/Curl │  HTTP    │  EdgeTtsServer   │ WebSocket│  Microsoft Edge │
│   Client    ├─────────►│  (localhost:5555)├─────────►│  TTS Service    │
└─────────────┘          └──────────────────┘          └─────────────────┘
                                  │
                                  ▼
                         ┌─────────────────┐
                         │  Cache + Locks  │
                         └─────────────────┘
```

## Microsoft Edge TTS WebSocket API

**Endpoint:**
```
wss://api.msedgeservices.com/tts/cognitiveservices/websocket/v1
```

**API Key (z Edge browser extension):**
```
6A5AA1D4EAFF4E9FB37E23D68491D6F4
```

**Parametry:**
- `Ocp-Apim-Subscription-Key`: API klíč pro autentizaci
- WebSocket protokol: `synthesize`

## Použití

### Jako HTTP API

```bash
# Jednoduché volání
curl -X POST http://localhost:5555/speak \
  -H "Content-Type: application/json" \
  -d '{"text":"Ahoj světe"}'

# S vlastním hlasem a rychlostí
curl -X POST http://localhost:5555/speak \
  -H "Content-Type: application/json" \
  -d '{"text":"Rychlá zpráva", "voice":"cs-CZ-AntoninNeural", "rate":"+50%"}'
```

### Z bash skriptu

```bash
#!/bin/bash
TEXT="$1"
curl -s -X POST http://localhost:5555/speak \
  -H "Content-Type: application/json" \
  -d "{\"text\":\"$TEXT\"}" > /dev/null
```

## Instalace

### 1. Build projekt

```bash
cd /home/jirka/projects/EdgeTtsWebSocketServer/EdgeTtsWebSocketServer
dotnet build -c Release
```

### 2. Vytvořit systemd službu

```bash
sudo nano /etc/systemd/system/edge-tts-server.service
```

```ini
[Unit]
Description=Edge TTS WebSocket Server
After=network.target

[Service]
Type=simple
User=jirka
WorkingDirectory=/home/jirka/projects/EdgeTtsWebSocketServer/EdgeTtsWebSocketServer
ExecStart=/usr/bin/dotnet run --project /home/jirka/projects/EdgeTtsWebSocketServer/EdgeTtsWebSocketServer
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

### 3. Spustit službu

```bash
sudo systemctl daemon-reload
sudo systemctl enable edge-tts-server
sudo systemctl start edge-tts-server
sudo systemctl status edge-tts-server
```

## Konfigurace

Upravte `appsettings.json`:

```json
{
  "EdgeTts": {
    "Port": 5555,
    "DefaultVoice": "cs-CZ-AntoninNeural",
    "DefaultRate": "+20%",
    "CacheDirectory": "~/.cache/edge-tts-server",
    "MicrophoneLockFile": "/tmp/microphone-active.lock",
    "SpeechLockFile": "/tmp/speech.lock"
  }
}
```

## API Endpoints

### POST /speak

Převede text na řeč a přehraje ho.

**Request:**
```json
{
  "text": "Text k převodu",
  "voice": "cs-CZ-AntoninNeural",  // volitelné
  "rate": "+20%",                    // volitelné
  "volume": "+0%",                   // volitelné
  "pitch": "+0Hz",                   // volitelné
  "play": true                       // volitelné, default: true
}
```

**Response:**
```json
{
  "success": true,
  "message": "✅ Played from cache: Text k převodu",
  "cached": true
}
```

### GET /voices

Vrátí seznam dostupných hlasů.

### DELETE /cache

Vymaže cache.

## Technické detaily

- **Framework**: ASP.NET Core 9.0
- **WebSocket Client**: System.Net.WebSockets
- **Audio Player**: ffplay (z ffmpeg)
- **Cache**: Filesystem-based s MD5 hash
- **Locks**: File-based locking pomocí FileStream

## Autor

Vytvořeno pro CML (Centrální Mozek Lidstva) systém.

## Licence

MIT License
