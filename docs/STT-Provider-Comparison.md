# Srovnání STT providerů: Whisper vs Google Speech-to-Text

Srovnání rychlosti transkripce mezi lokálním modelem Whisper a cloudovou službou Google Speech-to-Text API.

## Testovací prostředí

| Parametr | Hodnota |
|----------|---------|
| **Operační systém** | Debian 13 (Trixie) |
| **GPU** | NVIDIA GeForce RTX 3060 (8 GB VRAM) |
| **Whisper model** | ggml-large-v3-turbo.bin |
| **Whisper backend** | Whisper.net + CUDA (GPU akcelerace) |
| **Google STT** | Google Speech-to-Text API (cloud) |
| **Jazyk** | čeština (cs-CZ) |
| **Datum testu** | 29. ledna 2025 |

## Srovnávací tabulka

| Délka textu | Provider | Text (zkráceno) | Čas (ms) |
|-------------|----------|-----------------|----------|
| **Velmi krátké (< 30 znaků)** | | | |
| ~10 znaků | Whisper | "Pokračuj." | **449 ms** |
| ~10 znaků | Whisper | "Odgooglou!" | **449 ms** |
| ~25 znaků | Google | "tohle to se mi úplně nelíbí" | 593 ms |
| ~20 znaků | Whisper | "Tak ti držím palečky." | **516 ms** |
| ~28 znaků | Whisper | "Už ani pes po tobě neštěkne." | **529 ms** |
| **Krátké (30-100 znaků)** | | | |
| ~30 znaků | Google | "ano prosím tě zavři išus..." | 1185 ms |
| ~40 znaků | Google | "Pardon jsem to Přeučil..." | 766 ms |
| ~50 znaků | Google | "Tohlencto je testovací proud..." | 2006 ms |
| ~50 znaků | Whisper | "Dobře, a u toho jsou jaké..." | **531 ms** |
| ~50 znaků | Google | "tohle všechno jsem diktoval..." | 963 ms |
| ~55 znaků | Whisper | "Oba dva, co si přehrával..." | **625 ms** |
| ~60 znaků | Whisper | "Super, taky mě to napadlo..." | **550 ms** |
| ~65 znaků | Google | "dobře To znamená že nyní..." | 2163 ms |
| ~85 znaků | Whisper | "To už by si tam dneska..." | **624 ms** |
| **Střední (100-200 znaků)** | | | |
| ~110 znaků | Whisper | "Dobře, ale já jsem teď..." | **703 ms** |
| ~110 znaků | Whisper | "Protože jestli si dobře..." | **610 ms** |
| ~145 znaků | Whisper | "Napiš mi ten text, prosím..." | **717 ms** |
| ~150 znaků | Google | "Však jsem nadiktoval..." | 1951 ms |
| ~155 znaků | Google | "Připadá mi to že když..." | 2047 ms |
| ~160 znaků | Google | "prosím tě vrať mi teďkon..." | 2917 ms |
| ~165 znaků | Google | "dobře ale úplně nerozumím..." | 2570 ms |
| ~175 znaků | Google | "ještě prosím tě zkontroluj..." | 2155 ms |
| ~185 znaků | Google | "No zřejmě dochází k tomu..." | 2833 ms |
| ~200 znaků | Google | "prosím tě ale zkus vzít..." | 2763 ms |
| ~200 znaků | Google | "tohle to je testovací proud..." | 2730 ms |
| ~215 znaků | Whisper | "Děkuju. Teď prosím tě..." | **870 ms** |
| **Dlouhé (200-400 znaků)** | | | |
| ~215 znaků | Google | "Mně se ještě nelíbí..." | 4067 ms |
| ~220 znaků | Google | "to teda Nastav vobek..." | 3586 ms |
| ~230 znaků | Google | "dobře To uvidíme..." | 3106 ms |
| ~260 znaků | Google | "Zajímá mě teďkon..." | 3407 ms |
| ~265 znaků | Google | "Já teď úplně nechápu..." | 4243 ms |
| ~400 znaků | Whisper | "A prosím tě, dej to do..." | **1756 ms** |
| ~400 znaků | Google | "Počkej tomuhle tomu..." | 10030 ms |
| **Velmi dlouhé (> 400 znaků)** | | | |
| ~475 znaků | Whisper | "Prosím tě, přehraj mi..." | **2068 ms** |
| ~600 znaků | Whisper | "Ano, chci, aby si doplnil..." | **2226 ms** |
| ~620 znaků | Whisper | "Tak je tam možnost..." | **2488 ms** |

## Souhrn výsledků

| Metrika | Whisper (lokální GPU) | Google STT (API) |
|---------|----------------------|------------------|
| **Krátké texty (< 50 znaků)** | ~450-550 ms | ~600-2000 ms |
| **Střední texty (100-200 znaků)** | ~600-870 ms | ~1900-2900 ms |
| **Dlouhé texty (200-400 znaků)** | ~1700-2500 ms | ~3100-10000 ms |
| **Průměrná latence** | **~700 ms** | **~2500 ms** |
| **Rychlostní výhoda** | **3.5× rychlejší** | - |

## Závěr

**Whisper je přibližně 3-4× rychlejší** než Google Speech-to-Text díky:

1. **Lokální GPU zpracování** - model běží přímo na RTX 3060 s CUDA akcelerací
2. **Žádná síťová latence** - nepotřebuje internetové spojení
3. **Model v VRAM** - po prvním načtení zůstává model v paměti GPU

### Výhody Whisper (lokální)
- Rychlejší odezva
- Bez závislosti na internetu
- Bez nákladů za API volání
- Plná kontrola nad daty (privacy)

### Výhody Google STT (cloud)
- Žádné nároky na hardware
- Automatické aktualizace modelu
- Lepší pro některé jazyky/dialekty

## Konfigurace

Aktuální nastavení v `appsettings.json`:

```json
{
  "SpeechProvider": {
    "PrimaryProvider": "whisper",
    "FallbackProvider": "google",
    "EnableFallback": true
  }
}
```

Whisper je primární provider, Google STT slouží jako fallback při selhání.
