# opencode-notify-plugin

OpenCode plugin for notifying VirtualAssistant service. Instead of speaking directly (like the voice plugin), this plugin sends notifications to VirtualAssistant which handles the TTS output.

## Installation

```bash
cd plugins/opencode-notify-plugin
npm install
npm run build
```

## Configuration

Add to your `~/.config/opencode/opencode.json`:

```json
{
  "plugins": {
    "notify": "/path/to/VirtualAssistant/plugins/opencode-notify-plugin/dist/index.js"
  }
}
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OPENCODE_NOTIFY_URL` | `http://localhost:5055/api/tts/notify` | VirtualAssistant API endpoint |

## Usage

The plugin provides a `notify` tool that the AI can use:

```
notify({ text: "Úkol byl dokončen." })
```

## Architecture

```
OpenCode AI → notify tool → VirtualAssistant API → TTS output
```

## License

MIT
