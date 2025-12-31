# Issue #466: Remove STT Service menu item and fix Dictation toggle

## Problem Summary

After migrating SpeechToText microservice to inline Whisper.net (#457), two issues remain:

1. **STT Service menu item is obsolete** - Menu shows "STT Service - Zapnout/Vypnout" for non-existent external service
2. **Dictation toggle doesn't work** - Menu item exists but has no effect on dictation functionality

## Root Cause Analysis

### Problem 1: STT Service Menu Item

The menu still references the external `speech-to-text.service` which no longer exists:

**Affected Files:**
- `VirtualAssistantDBusMenuHandler.cs:25` - `SpeechToTextServiceId` constant
- `VirtualAssistantDBusMenuHandler.cs:56-61` - Event declarations
- `VirtualAssistantDBusMenuHandler.cs:96-97` - Status fields
- `VirtualAssistantDBusMenuHandler.cs:192-203` - UpdateSpeechToTextStatus method
- `VirtualAssistantDBusMenuHandler.cs:283-285, 298, 369-376, 441-443, 469-474, 573-583` - Menu layout code
- `SpeechToTextServiceManager.cs` - Entire file managing external service
- `ISpeechToTextServiceManager.cs` - Interface for external service
- `TrayServicesExtensions.cs:67-68` - DI registration
- `ServiceLifecycleManager.cs:14, 32-105` - Manager methods and dependency
- `TrayCoordinatorService.cs:93-94` - Event wiring

### Problem 2: Dictation Toggle Not Working

**Root Cause:** DictationWorker implements IDictationControl but is NOT registered in DI as the interface.

**Current State:**
```csharp
// WorkerServicesExtensions.cs:33-91
services.AddSingleton(sp => {
    // ... creates DictationWorker instance
    return new DictationWorker(...);
});

// WorkerServicesExtensions.cs:94
services.AddHostedService(sp => sp.GetRequiredService<DictationWorker>());

// ❌ MISSING: IDictationControl registration
```

**Impact:**
```csharp
// MenuEventDispatcher.cs:19
private readonly IDictationControl? _dictationControl;  // ❌ Always null!

// MenuEventDispatcher.cs:163-166
if (_dictationControl == null)  // ✅ This condition is ALWAYS true
{
    _logger.LogWarning("Dictation control not available");
    return;
}
```

## Solution Design

### Part 1: Remove STT Service References

**Files to modify:**
1. `VirtualAssistantDBusMenuHandler.cs` - Remove menu item and events
2. `ServiceLifecycleManager.cs` - Remove STT methods
3. `TrayCoordinatorService.cs` - Remove event wiring
4. `TrayServicesExtensions.cs` - Remove DI registration

**Files to delete:**
1. `SpeechToTextServiceManager.cs`
2. `ISpeechToTextServiceManager.cs`

**Changes in VirtualAssistantDBusMenuHandler.cs:**
- Remove `SpeechToTextServiceId` constant (line 25)
- Remove `OnStopSpeechToTextRequested` event (line 56)
- Remove `OnStartSpeechToTextRequested` event (line 61)
- Remove `UpdateSpeechToTextStatus()` method (lines 192-203)
- Remove `_sttServiceStatus` and `_sttServiceVersion` fields (lines 96-97)
- Remove all menu layout code for SpeechToTextServiceId
- Remove event handling in OnEventAsync (lines 573-583)

**Changes in ServiceLifecycleManager.cs:**
- Remove `ISpeechToTextServiceManager?` dependency (line 14)
- Remove constructor parameter (line 24)
- Remove `HandleStartSpeechToTextAsync()` method (lines 32-55)
- Remove `HandleStopSpeechToTextAsync()` method (lines 60-83)
- Remove `RefreshSpeechToTextStatusAsync()` method (lines 88-105)

**Changes in TrayCoordinatorService.cs:**
- Remove STT event wiring (lines 93-94)

**Changes in TrayServicesExtensions.cs:**
- Remove ISpeechToTextServiceManager registration (lines 67-68)
- Remove sttManager dependency from ServiceLifecycleManager (line 74)
- Update ServiceLifecycleManager constructor call (line 76)

### Part 2: Fix Dictation Toggle

**Single-line fix in WorkerServicesExtensions.cs:**

Add after line 91 (after DictationWorker singleton registration):

```csharp
// Register the same instance as IDictationControl interface
services.AddSingleton<IDictationControl>(sp => sp.GetRequiredService<DictationWorker>());
```

**Why this works:**
- DictationWorker is already registered as singleton (lines 33-91)
- This creates an alias registration for the same instance
- MenuEventDispatcher will now receive the actual DictationWorker instance
- `SetDictationEnabled()` will be called on the real worker

## Implementation Plan

1. ✅ Create GitHub issue (#466)
2. ✅ Analyze code and document root causes
3. Remove STT Service references:
   - Update VirtualAssistantDBusMenuHandler.cs
   - Update ServiceLifecycleManager.cs
   - Update TrayCoordinatorService.cs
   - Update TrayServicesExtensions.cs
   - Delete SpeechToTextServiceManager.cs
   - Delete ISpeechToTextServiceManager.cs
4. Fix Dictation toggle:
   - Add IDictationControl registration in WorkerServicesExtensions.cs
5. Test changes:
   - Build project
   - Run tests
   - Deploy and verify menu shows correct items
   - Verify dictation toggle actually works

## Expected Outcome

### Menu Before Fix:
```
VirtualAssistant - poslouchám
❌ STT Service - Zapnout          ← REMOVE THIS
✅ Diktace - Vypnout               ← FIX THIS (doesn't work)
✅ Posílání do LLM - Vypnout
...
```

### Menu After Fix:
```
VirtualAssistant - poslouchám
✅ Diktace - Vypnout               ← NOW WORKS!
✅ Posílání do LLM - Vypnout
...
```

## Testing Checklist

- [ ] Build succeeds without errors
- [ ] All tests pass
- [ ] Menu no longer shows STT Service item
- [ ] Dictation toggle changes menu label
- [ ] Dictation actually stops when toggled off
- [ ] CapsLock ignored when dictation disabled
- [ ] Dictation resumes when toggled back on
- [ ] No errors in logs related to STT service
