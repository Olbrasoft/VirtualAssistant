# Analýza architektury EdgeTTS - Problém a řešení

## 📊 Současný stav (ŠPATNĚ)

### Kde je co implementováno

```
VirtualAssistant projekt:
├── src/EdgeTtsWebSocketServer/              ❌ TĚD BY NEMĚLO BÝT!
│   ├── Services/EdgeTtsService.cs            ❌ WebSocket logika s Microsoft Edge TTS API
│   ├── Controllers/SpeechController.cs       ❌ HTTP API endpoint
│   ├── Models/SpeechRequest.cs              ❌ DTOs
│   └── Program.cs                           ❌ ASP.NET Core server
│
└── src/VirtualAssistant.Voice/
    └── PackageReferences:
        ├── Olbrasoft.TextToSpeech.Core (1.1.9)
        ├── Olbrasoft.TextToSpeech.Providers (1.1.9)      ✅ EdgeTtsProvider (HTTP klient)
        └── Olbrasoft.TextToSpeech.Orchestration (1.1.9)  ✅ Provider chain

TextToSpeech repository (GitHub):
└── src/TextToSpeech.Providers/
    └── EdgeTTS/
        ├── EdgeTtsProvider.cs                ✅ HTTP klient volající localhost:5555
        └── EdgeTtsConfiguration.cs           ✅ Konfigurace (BaseUrl, Voice, Rate...)
```

### Problém

**EdgeTtsWebSocketServer je ve VirtualAssistant projektu**, což znamená:

1. ❌ Při každé úpravě WebSocket logiky měníme VirtualAssistant
2. ❌ Nelze používat EdgeTTS v jiných projektech bez kopírování kódu
3. ❌ Verze EdgeTTS není verzována přes NuGet (není v balíčku)
4. ❌ Při update VirtualAssistant můžeme rozbít EdgeTTS implementaci
5. ❌ EdgeTtsWebSocketServer běží jako samostatná služba (systemd) mimo aplikaci

## 🎯 Očekávaný stav (SPRÁVNĚ)

### Ideální architektura

```
TextToSpeech repository:
├── src/TextToSpeech.Providers.EdgeTTS/       ✅ NOVÝ BALÍČEK
│   ├── EdgeTtsProvider.cs                    ✅ Upravený - volá přímo WebSocket
│   ├── EdgeTtsConfiguration.cs               ✅ Konfigurace (Voice, Rate, Pitch...)
│   ├── EdgeTtsWebSocketClient.cs             ✅ PŘESUNUTO z VirtualAssistant
│   └── Models/
│       ├── SsmlBuilder.cs                    ✅ Generování SSML
│       └── AudioDataParser.cs                ✅ Parsování binary messages
│
└── src/TextToSpeech.Providers.EdgeTTS.Server/ ✅ NOVÝ BALÍČEK (volitelný)
    ├── EdgeTtsHttpServer.cs                  ✅ Pro backward compatibility
    └── Program.cs                            ✅ Standalone HTTP server (pokud potřeba)

VirtualAssistant projekt:
└── src/VirtualAssistant.Voice/
    └── PackageReferences:
        ├── Olbrasoft.TextToSpeech.Core (1.2.0)
        ├── Olbrasoft.TextToSpeech.Providers.EdgeTTS (1.2.0)  ✅ Včetně WebSocket logiky
        └── Olbrasoft.TextToSpeech.Orchestration (1.2.0)
```

## 📋 Co přesunout z VirtualAssistant do TextToSpeech

### 1. WebSocket logika (KRITICKÉ)

**Soubor:** `EdgeTtsWebSocketServer/Services/EdgeTtsService.cs`

**Přesunout do:** `TextToSpeech/src/TextToSpeech.Providers.EdgeTTS/EdgeTtsWebSocketClient.cs`

**Obsahuje:**
- `GenerateAudioAsync()` - WebSocket komunikace s Microsoft
- `ConfigureWebSocketHeaders()` - User-Agent, MUID, compression
- `BuildWebSocketUri()` - Connection ID, Sec-MS-GEC token
- `SendSpeechConfigAsync()` - Config message
- `SendSsmlRequestAsync()` - SSML request
- `ReceiveAudioDataAsync()` - Příjem audio dat
- `ProcessBinaryMessage()` - Parsování audio chunks
- `GenerateSsml()` - SSML generování
- `DateToString()` - Timestamp formát
- `GenerateMuid()` - MUID generování
- `GenerateSecMsGec()` - Security token

### 2. Konstanty a konfigurace

**Přesunout:**
```csharp
// Z EdgeTtsService.cs do EdgeTtsConfiguration.cs
private const string BASE_URL = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";
private const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
private const string WSS_URL = $"wss://{BASE_URL}/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}";
private const string CHROMIUM_FULL_VERSION = "143.0.3650.75";
```

### 3. HTTP server (VOLITELNÉ - pro backward compatibility)

**Pokud chceme zachovat HTTP API:**
- Vytvořit samostatný balíček `TextToSpeech.Providers.EdgeTTS.Server`
- Přesunout `SpeechController.cs`, `Models/*`, `Program.cs`
- Použití: optional standalone server pro legacy integrace

## 🔄 Dva přístupy k řešení

### Přístup A: Direct WebSocket Provider (DOPORUČENO)

```csharp
// V TextToSpeech.Providers.EdgeTTS/EdgeTtsProvider.cs
public async Task<TtsResult> GenerateSpeechAsync(TtsRequest request)
{
    using var client = new EdgeTtsWebSocketClient(_configuration);

    var audioData = await client.GenerateAsync(
        request.Text,
        request.Voice,
        request.Rate,
        request.Pitch
    );

    return TtsResult.Ok(audioData);
}
```

**Výhody:**
- ✅ Přímá komunikace, žádný mezičlánek
- ✅ Rychlejší (bez HTTP overhead)
- ✅ Jednodušší architektura
- ✅ Méně procesů (není potřeba EdgeTtsWebSocketServer služba)

**Nevýhody:**
- ⚠️ Breaking change (verze 2.0.0)
- ⚠️ Nutnost upravit konfiguraci

### Přístup B: Hybrid (HTTP + WebSocket)

```csharp
// V EdgeTtsConfiguration.cs
public enum EdgeTtsMode
{
    WebSocket,  // Přímá komunikace (výchozí)
    Http        // Přes HTTP server (legacy)
}

public EdgeTtsMode Mode { get; set; } = EdgeTtsMode.WebSocket;
public string? HttpServerUrl { get; set; }  // Pouze pro HTTP mode
```

**Výhody:**
- ✅ Backward compatibility
- ✅ Volba: přímý WebSocket nebo HTTP server
- ✅ Postupná migrace

**Nevýhody:**
- ⚠️ Složitější kód
- ⚠️ Dva code paths na údržbu

## 📦 Nová struktura balíčků

### TextToSpeech.Providers.EdgeTTS (1.2.0)

```
├── EdgeTtsProvider.cs                 # Hlavní provider (volá WebSocketClient)
├── EdgeTtsConfiguration.cs            # Konfigurace
├── EdgeTtsWebSocketClient.cs          # WebSocket komunikace s Microsoft
├── Models/
│   ├── SsmlBuilder.cs                # SSML generování
│   ├── AudioDataParser.cs            # Parsování audio
│   └── WebSocketMessage.cs           # WebSocket message DTOs
└── Extensions/
    └── ServiceCollectionExtensions.cs # DI registrace
```

### Použití ve VirtualAssistant

```csharp
// appsettings.json
{
  "TTS": {
    "EdgeTTS": {
      "Voice": "cs-CZ-AntoninNeural",
      "Rate": "+10%",
      "Volume": "+0%",
      "Pitch": "+0Hz",
      "OutputFormat": "audio-24khz-96kbitrate-mono-mp3"
    }
  }
}

// Program.cs - ŽÁDNÁ ZMĚNA
services.AddTextToSpeech(configuration);  // Automaticky najde EdgeTtsProvider
```

## ✅ Výhody nové architektury

1. **Verzování:** EdgeTTS má vlastní verzi v NuGet (1.2.0, 1.3.0...)
2. **Znovupoužitelnost:** Jakýkoli projekt může použít EdgeTTS přes NuGet
3. **Stabilita:** Update VirtualAssistant nerozbije EdgeTTS
4. **Testovatelnost:** EdgeTTS lze testovat samostatně
5. **Deployment:** Není potřeba EdgeTtsWebSocketServer jako samostatná služba
6. **Konfigurace:** Pouze v appsettings.json (žádný HTTP server URL)

## 🚀 Migrace krok za krokem

### Fáze 1: Přesun do TextToSpeech repozitáře

1. Clone TextToSpeech repository lokálně
2. Vytvořit `src/TextToSpeech.Providers.EdgeTTS/` projekt
3. Přesunout `EdgeTtsService.cs` → `EdgeTtsWebSocketClient.cs`
4. Upravit `EdgeTtsProvider.cs` - volat WebSocketClient místo HTTP
5. Napsat unit testy
6. Publikovat NuGet balíček 1.2.0

### Fáze 2: Update VirtualAssistant

1. Update package reference: `Olbrasoft.TextToSpeech.Providers.EdgeTTS` na 1.2.0
2. Odstranit `EdgeTtsWebSocketServer` projekt
3. Upravit `appsettings.json` (odstranit EdgeTtsServer:BaseUrl)
4. Odstranit systemd service `edge-tts-server.service`
5. Testovat

### Fáze 3: Cleanup

1. Smazat `src/EdgeTtsWebSocketServer/` ze VirtualAssistant
2. Update dokumentace
3. Commit a push

## 📝 Shrnutí

**Problém:**
- EdgeTTS WebSocket logika je přímo ve VirtualAssistant projektu
- EdgeTtsWebSocketServer běží jako samostatná systemd služba
- Není to v TextToSpeech NuGet balíčku

**Řešení:**
- Přesunout WebSocket logiku do TextToSpeech.Providers.EdgeTTS balíčku
- EdgeTtsProvider volá přímo WebSocket (ne HTTP)
- VirtualAssistant jen používá NuGet balíček (žádný lokální kód)
- Odstranit EdgeTtsWebSocketServer službu

**Výsledek:**
- ✅ EdgeTTS je zapouzdřený v NuGet balíčku
- ✅ VirtualAssistant nemůže rozbít EdgeTTS implementaci
- ✅ Konfigurace jen přes appsettings.json
- ✅ Žádné externí služby (edge-tts-server)
