# Proof of Concept: Integrace Antigravity do VirtualAssistant

Tento dokument analyzuje současný stav zpracování notifikací ve `VirtualAssistant` a definuje kroky potřebné pro plnou podporu notifikací z IDE **Antigravity**.

## 1. Analýza současného stavu

VirtualAssistant přijímá notifikace přes REST API endpoint, který je následně zpracován pomocí CQRS patternu. Identita odesílatele (agenta) je pevně svázána s číselným ID v databázi.

### Komponenty
*   **API Endpoint:** `POST /api/notifications` (`NotificationsController`)
*   **DTO:** `CreateNotificationRequest` (obsahuje `Text`, `Source`, `IssueIds`)
*   **Command:** `CreateNotificationCommand`
*   **Handler:** `CreateNotificationCommandHandler`
*   **Enum:** `AgentType` (definuje mapování ID)
*   **Databáze:** Tabulka `agents` obsahuje předdefinované řádky pro povolené agenty.

### Současné mapování (AgentType.cs)
Aktuálně jsou podporováni pouze tito agenti (viz `AgentType` enum a migrace `SeedAgentsWithExplicitIds`):
*   `OpenCode` (ID: 1)
*   `ClaudeCode` (ID: 4)
*   `Gemini` (ID: 11)

### Logika mapování
V souboru `CreateNotificationCommandHandler.cs` metoda `MapAgentNameToType` provádí striktní mapování stringu na enum. Pokud název agenta není v seznamu, vyhodí výjimku.

```csharp
private static AgentType MapAgentNameToType(string agentName)
{
    var normalized = agentName.ToLowerInvariant().Trim();
    return normalized switch
    {
        "opencode" => AgentType.OpenCode,
        "claude" or "claude-code" => AgentType.ClaudeCode,
        "gemini" => AgentType.Gemini,
        _ => throw new ArgumentException(...) // Antigravity zde selže
    };
}
```

## 2. Plán implementace (Proof of Concept)

Pro zprovoznění notifikací z Antigravity je nutné provést změny ve třech vrstvách: definice dat, aplikační logika a databáze.

### Krok 1: Rozšíření doménového modelu
Soubor: `src/VirtualAssistant.Data/Enums/AgentType.cs`

Je potřeba přidat novou položku pro Antigravity. Navrhuji ID **20**, aby byla rezerva pro budoucí agenty.

```csharp
public enum AgentType
{
    // ... existující ...
    Gemini = 11,
    
    /// <summary>
    /// Antigravity IDE agent (ID: 20)
    /// </summary>
    Antigravity = 20
}
```

### Krok 2: Úprava command handleru
Soubor: `src/VirtualAssistant.Data.EntityFrameworkCore/CommandHandlers/NotificationCommandHandlers/CreateNotificationCommandHandler.cs`

Rozšířit `switch` výraz o podporu řetězce "antigravity".

```csharp
return normalized switch
{
    "opencode" => AgentType.OpenCode,
    "claude" or "claude-code" => AgentType.ClaudeCode,
    "gemini" => AgentType.Gemini,
    "antigravity" => AgentType.Antigravity, // Nová větev
    _ => throw new ArgumentException(...)
};
```

### Krok 3: Databázová migrace
Je nutné vytvořit novou EF Core migraci, která vloží záznam pro Antigravity do tabulky `agents`.

Příkaz:
`dotnet ef migrations add SeedAntigravityAgent --project src/VirtualAssistant.Data.EntityFrameworkCore --startup-project src/VirtualAssistant.Service`

Obsah metody `Up()` v nové migraci by měl vypadat takto:

```csharp
migrationBuilder.InsertData(
    table: "agents",
    columns: new[] { "id", "created_at", "is_active", "label", "name" },
    values: new object[] { 20, DateTime.UtcNow, true, "agent:antigravity", "antigravity" }
);

// Aktualizace sekvence (pokud je potřeba, aby další auto-increment začínal výše)
migrationBuilder.Sql("SELECT setval('agents_id_seq', 20, true);");
```

## 3. Ověření funkčnosti

Po nasazení změn a aplikaci migrací lze integraci ověřit pomocí `curl`:

```bash
curl -X POST http://localhost:5055/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Hello from Antigravity POC",
    "source": "antigravity",
    "issueIds": [1]
  }'
```

Očekávaná odpověď: `200 OK` s JSON tělem obsahujícím ID nové notifikace.

```