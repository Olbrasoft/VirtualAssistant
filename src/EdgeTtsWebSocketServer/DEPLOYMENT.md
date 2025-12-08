# EdgeTTS WebSocket Server - Deployment Guide

## System Integration

### 1. Systemd Service
- **Service name**: `edge-tts-server.service`
- **Status**: `systemctl status edge-tts-server`
- **Start/Stop**: `systemctl start|stop edge-tts-server`
- **Logs**: `journalctl -u edge-tts-server -f`
- **Port**: 5555
- **Auto-start**: Enabled on boot

### 2. API Wrapper Script
- **Location**: `~/cml/voice-output/tts-api.sh`
- **Usage**: `tts-api.sh "text to speak" [true|false]`
- **Example**: `tts-api.sh "Hello world" true`

### 3. Configuration
- **Cache directory**: `~/.cache/edge-tts-server/`
- **Lock files**: `/tmp/microphone-active.lock`, `/tmp/speech.lock`
- **Default voice**: `cs-CZ-AntoninNeural` (Czech male)
- **Default rate**: `+20%` (faster speech)

### 4. API Endpoints
- **POST /api/speech/speak** - Generate and play speech
  ```bash
  curl -X POST http://localhost:5555/api/speech/speak \
    -H "Content-Type: application/json" \
    -d '{"text":"Hello","play":true}'
  ```

- **DELETE /api/speech/cache** - Clear cache
  ```bash
  curl -X DELETE http://localhost:5555/api/speech/cache
  ```

- **GET /** - Health check
  ```bash
  curl http://localhost:5555
  ```

## Integration with CML

The TTS system is fully integrated into CML (Centrální Mozek Lidstva):
- OpenCode automatically uses `tts-api.sh` for voice output
- Background execution with `&` ensures non-blocking operation
- Cache system prevents redundant API calls
- Lock mechanism prevents audio conflicts

## Technical Details

### WebSocket Connection
- **URL**: `wss://api.msedgeservices.com/tts/cognitiveservices/websocket/v1`
- **Subprotocol**: `synthesize`
- **Authentication**: API key + DRM token (Sec-MS-GEC)
- **DRM**: Windows epoch with 5-minute rounding + SHA256

### Audio Format
- **Output**: MP3, 24kHz, 48kbps, mono
- **Player**: ffplay (from ffmpeg)

### Deployment
Built with .NET 9.0, runs as systemd service with user dotnet installation.
