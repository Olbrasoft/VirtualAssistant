# EdgeTTS Architektura - Vizuální diagramy

## 📊 SOUČASNÝ STAV (ŠPATNĚ)

```
┌─────────────────────────────────────────────────────────────────┐
│                    VirtualAssistant Service                      │
│                         (Port 5055)                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  VirtualAssistant.Voice                                         │
│  ├── Package: Olbrasoft.TextToSpeech.Providers (1.1.9)         │
│  │   └── EdgeTtsProvider.cs                                    │
│  │       └── Volá HTTP: http://localhost:5555/api/speech/speak │
│  │                                                              │
│  └── TtsService.cs                                              │
│      └── Používá EdgeTtsProvider přes ITtsProvider interface    │
│                                                                  │
└───────────────────────────┬──────────────────────────────────────┘
                            │
                            │ HTTP POST
                            │ {text, voice, rate}
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│              EdgeTtsWebSocketServer ❌ (PROBLÉM!)                │
│                    (Port 5555)                                   │
│            systemd: edge-tts-server.service                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Controllers/SpeechController.cs                                │
│  └── POST /api/speech/speak                                     │
│      └── Volá EdgeTtsService                                    │
│                                                                  │
│  Services/EdgeTtsService.cs ❌ (TĚD BY NEMĚLO BÝT!)             │
│  ├── ConfigureWebSocketHeaders()                               │
│  ├── BuildWebSocketUri()                                        │
│  ├── SendSpeechConfigAsync()                                    │
│  ├── SendSsmlRequestAsync()                                     │
│  ├── ReceiveAudioDataAsync()                                    │
│  └── ProcessBinaryMessage()                                     │
│                                                                  │
└───────────────────────────┬──────────────────────────────────────┘
                            │
                            │ WebSocket (wss://)
                            │ TrustedClientToken: 6A5AA1...
                            │ Connection ID, Sec-MS-GEC
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│              Microsoft Edge TTS WebSocket API                    │
│   wss://speech.platform.bing.com/consumer/speech/synthesize/    │
│                   readaloud/edge/v1                              │
└─────────────────────────────────────────────────────────────────┘
```

### Problémy současné architektury:

1. ❌ **Dva samostatné procesy**: VirtualAssistant + EdgeTtsWebSocketServer
2. ❌ **Dva systemd services**: virtual-assistant.service + edge-tts-server.service
3. ❌ **HTTP overhead**: Zbytečná serializace přes HTTP mezi procesy
4. ❌ **WebSocket logika v aplikaci**: EdgeTtsService.cs je součástí VirtualAssistant
5. ❌ **Nelze verzovat**: EdgeTTS implementace není v NuGet balíčku
6. ❌ **Deployment complexity**: Nutnost nasadit 2 služby místo 1

---

## 🎯 CÍLOVÝ STAV (SPRÁVNĚ)

### Varianta A: Direct WebSocket (DOPORUČENO)

```
┌─────────────────────────────────────────────────────────────────┐
│                    VirtualAssistant Service                      │
│                         (Port 5055)                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  VirtualAssistant.Voice                                         │
│  └── Package: Olbrasoft.TextToSpeech.Providers.EdgeTTS (1.2.0) │
│      └── EdgeTtsProvider.cs ✅                                  │
│          └── Používá EdgeTtsWebSocketClient ✅                  │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ NuGet: TextToSpeech.Providers.EdgeTTS (1.2.0) ✅          │ │
│  │                                                            │ │
│  │  EdgeTtsProvider.cs                                       │ │
│  │  └── GenerateSpeechAsync(request)                         │ │
│  │      └── new EdgeTtsWebSocketClient(config)               │ │
│  │          └── GenerateAsync(text, voice, rate)             │ │
│  │                                                            │ │
│  │  EdgeTtsWebSocketClient.cs ✅ (PŘESUNUTO)                 │ │
│  │  ├── ConfigureWebSocketHeaders()                          │ │
│  │  ├── BuildWebSocketUri()                                  │ │
│  │  ├── SendSpeechConfigAsync()                              │ │
│  │  ├── SendSsmlRequestAsync()                               │ │
│  │  ├── ReceiveAudioDataAsync()                              │ │
│  │  └── ProcessBinaryMessage()                               │ │
│  │                                                            │ │
│  │  EdgeTtsConfiguration.cs ✅                                │ │
│  │  ├── Voice: "cs-CZ-AntoninNeural"                         │ │
│  │  ├── Rate: "+10%"                                         │ │
│  │  ├── OutputFormat: "audio-24khz-96kbitrate-mono-mp3"     │ │
│  │  └── Constants: WSS_URL, TRUSTED_CLIENT_TOKEN            │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
└───────────────────────────┬──────────────────────────────────────┘
                            │
                            │ WebSocket (wss://)
                            │ Přímo z VirtualAssistant procesu
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│              Microsoft Edge TTS WebSocket API                    │
│   wss://speech.platform.bing.com/consumer/speech/synthesize/    │
└─────────────────────────────────────────────────────────────────┘
```

### Výhody cílové architektury:

1. ✅ **Jeden proces**: Pouze VirtualAssistant Service
2. ✅ **Jeden systemd service**: virtual-assistant.service
3. ✅ **Přímá komunikace**: WebSocket přímo z aplikace
4. ✅ **Zapouzdřeno v NuGet**: EdgeTTS je samostatný balíček
5. ✅ **Verzovatelné**: NuGet balíček má vlastní verzi (1.2.0, 1.3.0...)
6. ✅ **Jednoduchý deployment**: Nasazení jedné služby
7. ✅ **Znovupoužitelné**: Jakýkoli projekt může použít EdgeTTS NuGet

---

## 🔄 Migrace z A do B

### Krok 1: Příprava TextToSpeech repository

```
TextToSpeech (GitHub)
└── src/
    ├── TextToSpeech.Core/                    (existuje)
    ├── TextToSpeech.Providers/               (existuje)
    │   ├── Azure/
    │   ├── EdgeTTS/
    │   │   ├── EdgeTtsProvider.cs            (upravit)
    │   │   └── EdgeTtsConfiguration.cs       (rozšířit)
    │   ├── Google/
    │   └── VoiceRss/
    │
    └── TextToSpeech.Providers.EdgeTTS/       ✅ NOVÝ PROJEKT
        ├── EdgeTtsProvider.cs                ✅ Přepsat (volá WebSocket)
        ├── EdgeTtsConfiguration.cs           ✅ Přidat konstanty
        ├── EdgeTtsWebSocketClient.cs         ✅ PŘESUNOUT z VirtualAssistant
        ├── Models/
        │   ├── SsmlBuilder.cs                ✅ Generování SSML
        │   └── AudioDataParser.cs            ✅ Parsování binary messages
        └── Extensions/
            └── ServiceCollectionExtensions.cs ✅ DI registrace
```

### Krok 2: Publikace NuGet balíčku

```bash
cd ~/GitHub/Olbrasoft/TextToSpeech
cd src/TextToSpeech.Providers.EdgeTTS

# Build a pack
dotnet pack -c Release

# Publish na nuget.org
dotnet nuget push bin/Release/Olbrasoft.TextToSpeech.Providers.EdgeTTS.1.2.0.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### Krok 3: Update VirtualAssistant

```xml
<!-- VirtualAssistant.Voice.csproj -->
<ItemGroup>
  <!-- Starý balíček - ODSTRANIT -->
  <!-- <PackageReference Include="Olbrasoft.TextToSpeech.Providers" Version="1.1.9" /> -->

  <!-- Nový balíček - PŘIDAT -->
  <PackageReference Include="Olbrasoft.TextToSpeech.Providers.EdgeTTS" Version="1.2.0" />
  <PackageReference Include="Olbrasoft.TextToSpeech.Providers.Azure" Version="1.2.0" />
  <PackageReference Include="Olbrasoft.TextToSpeech.Providers.Google" Version="1.2.0" />
  <PackageReference Include="Olbrasoft.TextToSpeech.Providers.VoiceRSS" Version="1.2.0" />
</ItemGroup>
```

```json
// appsettings.json - ODSTRANIT EdgeTtsServer sekci
{
  "TTS": {
    // ODSTRANIT TOTO:
    // "EdgeTtsServer": {
    //   "BaseUrl": "http://localhost:5555"
    // },

    // EdgeTTS konfigurace zůstává:
    "EdgeTTS": {
      "Voice": "cs-CZ-AntoninNeural",
      "Rate": "+10%",
      "Volume": "+0%",
      "Pitch": "+0Hz",
      "OutputFormat": "audio-24khz-96kbitrate-mono-mp3"
    }
  }
}
```

### Krok 4: Cleanup

```bash
cd ~/Olbrasoft/VirtualAssistant

# Smazat EdgeTtsWebSocketServer projekt
rm -rf src/EdgeTtsWebSocketServer/

# Update solution file (odstranit EdgeTtsWebSocketServer)
# Upravit VirtualAssistant.sln

# Zastavit a odstranit systemd service
systemctl --user stop edge-tts-server.service
systemctl --user disable edge-tts-server.service
rm ~/.config/systemd/user/edge-tts-server.service
systemctl --user daemon-reload

# Smazat nasazený server
rm -rf ~/apps/edge-tts/

# Build a test
dotnet build
dotnet test
```

---

## 📦 Výsledná struktura balíčků

### TextToSpeech NuGet packages (verze 1.2.0)

```
Olbrasoft.TextToSpeech.Core (1.2.0)
├── Interfaces/
│   └── ITtsProvider.cs
└── Models/
    ├── TtsRequest.cs
    ├── TtsResult.cs
    └── AudioData.cs

Olbrasoft.TextToSpeech.Providers.Azure (1.2.0)
└── AzureTtsProvider.cs
    └── Microsoft.CognitiveServices.Speech SDK

Olbrasoft.TextToSpeech.Providers.EdgeTTS (1.2.0) ✅ NOVÝ
├── EdgeTtsProvider.cs
├── EdgeTtsWebSocketClient.cs ✅ WebSocket logika
└── EdgeTtsConfiguration.cs

Olbrasoft.TextToSpeech.Providers.Google (1.2.0)
└── GoogleTtsProvider.cs

Olbrasoft.TextToSpeech.Providers.VoiceRSS (1.2.0)
└── VoiceRssTtsProvider.cs

Olbrasoft.TextToSpeech.Orchestration (1.2.0)
└── TtsProviderChain.cs
    └── Circuit breaker, retry, fallback
```

### VirtualAssistant dependencies

```
VirtualAssistant.Voice
├── Olbrasoft.TextToSpeech.Core (1.2.0)
├── Olbrasoft.TextToSpeech.Providers.Azure (1.2.0)
├── Olbrasoft.TextToSpeech.Providers.EdgeTTS (1.2.0) ✅
├── Olbrasoft.TextToSpeech.Providers.Google (1.2.0)
├── Olbrasoft.TextToSpeech.Providers.VoiceRSS (1.2.0)
└── Olbrasoft.TextToSpeech.Orchestration (1.2.0)
```

---

## 🎯 Shrnutí změn

### Co se ODSTRANÍ z VirtualAssistant:

- ❌ `src/EdgeTtsWebSocketServer/` projekt (celý)
- ❌ `~/apps/edge-tts/` nasazení
- ❌ `edge-tts-server.service` systemd service
- ❌ `EdgeTtsServer:BaseUrl` konfigurace z appsettings.json

### Co se PŘIDÁ do TextToSpeech:

- ✅ `src/TextToSpeech.Providers.EdgeTTS/` nový projekt
- ✅ `EdgeTtsWebSocketClient.cs` (přesunuto z VirtualAssistant)
- ✅ `EdgeTtsProvider.cs` (přepsán - volá WebSocket místo HTTP)
- ✅ NuGet balíček `Olbrasoft.TextToSpeech.Providers.EdgeTTS 1.2.0`

### Co zůstane ve VirtualAssistant:

- ✅ Package reference na `Olbrasoft.TextToSpeech.Providers.EdgeTTS`
- ✅ Konfigurace v `appsettings.json` (Voice, Rate, OutputFormat...)
- ✅ `TtsService.cs` používá ITtsProvider (žádná změna kódu!)
