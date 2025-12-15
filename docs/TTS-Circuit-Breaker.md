# TTS Circuit Breaker

## Přehled

TTS systém ve VirtualAssistant používá **circuit breaker pattern** pro správu fallback logiky mezi poskytovateli TTS. Když jeden poskytovatel selže, systém automaticky přepne na další v pořadí a dočasně zablokuje selhávajícího poskytovatele.

## Jak to funguje

### Základní princip

```
Provider selže → MarkFailed() → cooldown (blokace)
                      ↓
    Další request během cooldownu → přeskočí providera → jde na fallback
                      ↓
    Po vypršení cooldownu → zkusí providera znovu
```

### Exponenciální backoff

Po každém selhání se cooldown zdvojnásobuje:

| Počet selhání | Cooldown |
|---------------|----------|
| 1 | 5 minut |
| 2 | 10 minut |
| 3 | 20 minut |
| 4 | 40 minut |
| 5+ | 60 minut (maximum) |

### Konfigurace

V `appsettings.json`:

```json
"TtsProviderChain": {
    "Providers": ["EdgeTTS-HTTP", "EdgeTTS", "AzureTTS", "VoiceRSS", "GoogleTTS", "PiperTTS"],
    "CircuitBreaker": {
        "FailureThreshold": 3,
        "CooldownMinutes": 5,
        "MaxCooldownMinutes": 60,
        "UseExponentialBackoff": true
    }
}
```

| Parametr | Výchozí | Popis |
|----------|---------|-------|
| `FailureThreshold` | 3 | Počet selhání před aktivací circuit breakeru |
| `CooldownMinutes` | 5 | Počáteční cooldown v minutách |
| `MaxCooldownMinutes` | 60 | Maximální cooldown v minutách |
| `UseExponentialBackoff` | true | Zdvojnásobuje cooldown po každém selhání |

## Dostupní poskytovatelé

| Provider | Jméno v configu | Popis |
|----------|-----------------|-------|
| `HttpEdgeTtsProvider` | `EdgeTTS-HTTP` | Lokální HTTP server (localhost:5555), nejspolehlivější |
| `EdgeTtsProvider` | `EdgeTTS` | Přímé WebSocket spojení na Microsoft |
| `AzureTtsProvider` | `AzureTTS` | Azure Cognitive Services (vyžaduje API klíč) |
| `VoiceRssProvider` | `VoiceRSS` | VoiceRSS služba |
| `GoogleTtsProvider` | `GoogleTTS` | Google TTS |
| `PiperTtsProvider` | `PiperTTS` | Offline fallback, hlas "Jirka" |

## API Endpointy

### Kontrola stavu providerů

```bash
curl http://localhost:5055/api/tts/providers
```

Vrací JSON se stavem každého providera včetně:
- `IsHealthy` - zda je provider dostupný
- `ConsecutiveFailures` - počet po sobě jdoucích selhání
- `NextRetryAt` - kdy se provider znovu zkusí

### Reset circuit breakeru

```bash
# Reset všech providerů
curl -X POST http://localhost:5055/api/tts/reset-circuit-breaker

# Reset konkrétního providera
curl -X POST "http://localhost:5055/api/tts/reset-circuit-breaker?provider=EdgeTTS"
```

## Implementace

### Klíčové soubory

| Soubor | Popis |
|--------|-------|
| `src/VirtualAssistant.Voice/Services/TtsProviderChain.cs` | Hlavní logika circuit breakeru |
| `src/VirtualAssistant.Voice/Configuration/TtsProviderChainOptions.cs` | Konfigurace |
| `src/VirtualAssistant.Voice/Services/ITtsProvider.cs` | Interface pro providery |

### Kontrola blokace (TtsProviderChain.cs)

```csharp
// Řádky 112-116
if (!state.IsHealthy && state.NextRetryAt.HasValue && DateTime.UtcNow < state.NextRetryAt.Value)
{
    _logger.LogDebug("Skipping provider '{Name}' - circuit breaker open until {RetryAt}",
        providerName, state.NextRetryAt.Value.ToLocalTime());
    continue;  // PŘESKOČÍ TOHOTO PROVIDERA
}
```

### Výpočet cooldownu (TtsProviderChain.cs)

```csharp
// Řádky 237-253
var cooldownMinutes = _options.CircuitBreaker.CooldownMinutes;

if (_options.CircuitBreaker.UseExponentialBackoff && state.ConsecutiveFailures > 1)
{
    cooldownMinutes = (int)Math.Min(
        cooldownMinutes * Math.Pow(2, state.ConsecutiveFailures - 1),
        _options.CircuitBreaker.MaxCooldownMinutes
    );
}

state.NextRetryAt = DateTime.UtcNow.AddMinutes(cooldownMinutes);
```

## Troubleshooting

### TTS používá fallback hlas místo Edge TTS

1. Zkontroluj stav providerů: `curl http://localhost:5055/api/tts/providers`
2. Ověř, že `EdgeTTS-HTTP` je v seznamu providerů v `appsettings.json`
3. Ověř, že Edge TTS server běží na portu 5555
4. Případně resetuj circuit breaker

### Provider je stále blokovaný

Circuit breaker může blokovat provider až 60 minut. Použij reset endpoint:

```bash
curl -X POST http://localhost:5055/api/tts/reset-circuit-breaker
```

### Jak zjistit který provider se používá

V logu hledej zprávy:
- `"Using TTS provider: {Name}"` - který provider se použil
- `"Skipping provider '{Name}' - circuit breaker open"` - provider přeskočen
- `"Provider '{Name}' failed: {Error}"` - provider selhal
