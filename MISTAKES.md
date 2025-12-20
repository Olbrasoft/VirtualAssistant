# Claude's Mistakes Log

Tento soubor obsahuje záznam chyb, které jsem udělal, abych je neopakoval.

## 2025-12-20: Zapomenutí secrets při deploymentu

**Chyba:**
- Při přesunu VirtualAssistant z `~/apps/` do `/opt/olbrasoft/` jsem zapomněl nakonfigurovat **secrets v production**
- Pouze jsem zkopíroval binárky a config, ale **NEZAPSAL JSEM SECRETS** do systemd EnvironmentFile
- Výsledek: AzureTTS nefunguje, protože `SubscriptionKey` je prázdný

**Co jsem měl udělat podle deployment-secrets-guide.md:**

1. **Zjistit jaké secrets aplikace potřebuje:**
   ```bash
   # V development
   dotnet user-secrets list --project src/VirtualAssistant.Service/

   # V appsettings.json - najít všechny prázdné klíče
   grep -r "Key\|Token\|Password\|Secret" appsettings.json | grep '""'
   ```

2. **Vytvořit systemd EnvironmentFile** (`~/.config/systemd/user/virtual-assistant.env`):
   ```bash
   # Secrets pro VirtualAssistant (NEVER COMMIT!)

   # Azure TTS
   AzureTTS__SubscriptionKey=xxxxx

   # Database
   ConnectionStrings__DefaultConnection=Host=localhost;Database=virtual_assistant;Username=postgres;Password=xxxxx

   # GitHub
   GitHub__Token=ghp_xxxxx

   # LLM Providers (načítat z ApiKeysFile)
   # (tyto jsou v ~/Dokumenty/přístupy/*.txt)
   ```

3. **Přidat EnvironmentFile do systemd service:**
   ```ini
   [Service]
   EnvironmentFile=%h/.config/systemd/user/virtual-assistant.env
   ```

4. **Restart service:**
   ```bash
   systemctl --user daemon-reload
   systemctl --user restart virtual-assistant.service
   ```

**Pravidlo do budoucna:**
> **PŘED oznámením "deployment completed" VŽDY zkontroluj:**
> 1. ✅ Binárky nasazené
> 2. ✅ Konfigurace zkopírována
> 3. ⚠️ **SECRETS nakonfigurované v EnvironmentFile**
> 4. ✅ Service restartovaný
> 5. ✅ Logy zkontrolované - ŽÁDNÉ chyby typu "not configured"

**Jak zjistit že secrets NEJSOU nakonfigurované:**
```bash
# Kontrola logů - nesmí tam být "not configured", "not available"
journalctl --user -u virtual-assistant.service -n 100 | grep -i "not configured\|not available\|failed.*config"
```

Pokud najdu takové chyby → secrets CHYBÍ → OPRAVIT PŘED dokončením deploymentu!

---

## Template pro budoucí chyby

**Chyba:**
[Popis co jsem udělal špatně]

**Co jsem měl udělat:**
[Správný postup podle dokumentace]

**Pravidlo do budoucna:**
> [Konkrétní pravidlo jak se tomu vyhnout]

**Jak zjistit problém:**
```bash
[Příkazy pro detekci]
```
