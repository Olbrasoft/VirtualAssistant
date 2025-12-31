# SpeechToText Migration Analysis

**Issue**: #458 - Analysis & Planning
**Parent Issue**: #457 - Merge SpeechToText microservice into VirtualAssistant
**Date**: 2025-12-31

## Executive Summary

This document analyzes the migration of SpeechToText microservice into VirtualAssistant as an inline Whisper.net implementation. The migration will replace gRPC calls to a separate service with direct Whisper.net transcription.

## 1. Interface Mapping

### Current State (Microservice)

**SpeechToText.Core.Interfaces.ITranscriptionProvider**
```csharp
public interface ITranscriptionProvider
{
    string Name { get; }
    Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default);
    Task<TranscriptionProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default);
}
```

**VirtualAssistant.Core.Speech.ISpeechTranscriber** (gRPC client adapter)
```csharp
public interface ISpeechTranscriber : IDisposable
{
    string Language { get; }
    Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default);
    Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default);
}
```

### Interface Differences

| Feature | ITranscriptionProvider | ISpeechTranscriber | Notes |
|---------|------------------------|-------------------|-------|
| **Disposal** | ❌ No | ✅ IDisposable | WhisperNetProvider already implements IDisposable |
| **Name Property** | ✅ string Name | ❌ No | Not needed (single provider) |
| **Language Property** | ❌ No | ✅ string Language | Required by VirtualAssistant |
| **Request Model** | TranscriptionRequest | byte[] or Stream | VirtualAssistant uses simple byte[] |
| **Info Method** | GetInfoAsync() | ❌ No | Not used by VirtualAssistant |

### Adaptation Strategy

**Option A**: Create `WhisperNetTranscriber` adapter class
- Implements ISpeechTranscriber
- Wraps WhisperNetProvider
- Maps byte[] → TranscriptionRequest
- Adds Language property from configuration

**Option B**: Modify WhisperNetProvider directly
- Add ISpeechTranscriber implementation
- Keep ITranscriptionProvider for backward compatibility
- Dual interface support

**✅ Recommended**: **Option A** - Clean adapter pattern, preserves SpeechToText.Core unchanged.

## 2. Model Mapping

### TranscriptionRequest

**SpeechToText.Core.Models.TranscriptionRequest**
```csharp
public class TranscriptionRequest : IValidatableObject
{
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public string? Language { get; set; }
    public string? PreferredProvider { get; set; }
    public string? ModelName { get; set; }

    // Validation: MaxAudioSizeBytes = 10 MB
}
```

**VirtualAssistant → SpeechToText Mapping**
```csharp
// VirtualAssistant call:
ISpeechTranscriber.TranscribeAsync(byte[] audioData, CancellationToken ct)

// Maps to:
new TranscriptionRequest
{
    AudioData = audioData,
    Language = Language, // from ISpeechTranscriber.Language property
    ModelName = null     // use configured default
}
```

### TranscriptionResult

Both projects have **similar but incompatible** TranscriptionResult classes:

| Property | SpeechToText.Core | VirtualAssistant.Core | Notes |
|----------|-------------------|----------------------|-------|
| **Text** | ✅ string | ✅ string | Same |
| **Success** | ✅ bool | ✅ bool | Same |
| **ErrorMessage** | ✅ string? | ✅ string? | Same |
| **Confidence** | ✅ float? | ✅ float | VirtualAssistant: non-nullable |
| **Language** | ✅ string? | ❌ No | Extra in SpeechToText |
| **ProviderUsed** | ✅ string? | ❌ No | Extra in SpeechToText |
| **AudioDuration** | ✅ TimeSpan? | ❌ No | Extra in SpeechToText |
| **TranscriptionTime** | ✅ TimeSpan? | ❌ No | Extra in SpeechToText |
| **OriginalText** | ❌ No | ✅ string? | VirtualAssistant-specific (LLM filtering) |
| **FilteredText** | ❌ No | ✅ string? | VirtualAssistant-specific (LLM filtering) |
| **LlmDurationMs** | ❌ No | ✅ int? | VirtualAssistant-specific (LLM correction) |

**Result Mapping Strategy**:
```csharp
// SpeechToText.Core.TranscriptionResult → VirtualAssistant.Core.TranscriptionResult
if (sttResult.Success)
{
    return new VirtualAssistant.Core.Speech.TranscriptionResult(
        sttResult.Text,
        sttResult.Confidence ?? 1.0f
    );
}
else
{
    return new VirtualAssistant.Core.Speech.TranscriptionResult(
        sttResult.ErrorMessage ?? "Unknown error"
    );
}
```

## 3. Dependency Analysis

### NuGet Packages to ADD to VirtualAssistant.Voice

From `SpeechToText.Providers.csproj`:
```xml
<PackageReference Include="Whisper.net" Version="1.8.0" />
<PackageReference Include="Whisper.net.Runtime.Cuda.Linux" Version="1.8.0" />
```

Already present in VirtualAssistant.Voice:
```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="10.0.1" />
```

### NuGet Packages to REMOVE from VirtualAssistant.Voice

```xml
<PackageReference Include="Google.Protobuf" Version="3.33.2" />
<PackageReference Include="Grpc.Net.Client" Version="2.76.0" />
<PackageReference Include="Grpc.Tools" Version="2.76.0" />
<Protobuf Include="Protos/speech_to_text.proto" GrpcServices="Client" />
```

### Files to DELETE from VirtualAssistant

- `src/VirtualAssistant.Voice/Services/SpeechToTextGrpcClient.cs`
- `src/VirtualAssistant.Voice/Protos/speech_to_text.proto`

## 4. Files to Migrate

### From SpeechToText.Core → VirtualAssistant.Voice

Copy to `src/VirtualAssistant.Voice/SpeechToText/`:

| Source File | Target File | Purpose |
|-------------|-------------|---------|
| `SpeechToText.Core/Configuration/WhisperModelLocator.cs` | `SpeechToText/WhisperModelLocator.cs` | FHS-compliant model path resolution |
| `SpeechToText.Core/Configuration/SpeechToTextOptions.cs` | `SpeechToText/WhisperOptions.cs` | Configuration (rename to avoid conflict) |
| `SpeechToText.Core/Interfaces/ITranscriptionProvider.cs` | `SpeechToText/ITranscriptionProvider.cs` | Interface for provider pattern |
| `SpeechToText.Core/Models/TranscriptionRequest.cs` | `SpeechToText/TranscriptionRequest.cs` | Request model with validation |
| `SpeechToText.Core/Models/TranscriptionResult.cs` | `SpeechToText/TranscriptionResult.cs` | Result model (rename to avoid conflict) |
| `SpeechToText.Core/Models/TranscriptionProviderInfo.cs` | `SpeechToText/TranscriptionProviderInfo.cs` | Provider info model |

**Namespace Changes**:
- Old: `Olbrasoft.SpeechToText.Core.*`
- New: `Olbrasoft.VirtualAssistant.Voice.SpeechToText.*`

### From SpeechToText.Providers → VirtualAssistant.Voice

Copy to `src/VirtualAssistant.Voice/SpeechToText/`:

| Source File | Target File | Purpose |
|-------------|-------------|---------|
| `SpeechToText.Providers/WhisperNetProvider.cs` | `SpeechToText/WhisperNetProvider.cs` | Core Whisper.net implementation with model caching |

## 5. New Adapter Class

Create `src/VirtualAssistant.Voice/SpeechToText/WhisperNetTranscriber.cs`:

```csharp
namespace Olbrasoft.VirtualAssistant.Voice.SpeechToText;

/// <summary>
/// Adapter that wraps WhisperNetProvider to implement ISpeechTranscriber.
/// Replaces SpeechToTextGrpcClient with direct Whisper.net transcription.
/// </summary>
public sealed class WhisperNetTranscriber : ISpeechTranscriber
{
    private readonly ITranscriptionProvider _provider;
    private readonly string _language;

    public WhisperNetTranscriber(ITranscriptionProvider provider, string language)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _language = language ?? "cs";
    }

    public string Language => _language;

    public async Task<VirtualAssistant.Core.Speech.TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        var request = new TranscriptionRequest
        {
            AudioData = audioData,
            Language = _language
        };

        var result = await _provider.TranscribeAsync(request, cancellationToken);

        // Map SpeechToText result → VirtualAssistant result
        if (result.Success)
        {
            return new VirtualAssistant.Core.Speech.TranscriptionResult(
                result.Text,
                result.Confidence ?? 1.0f
            );
        }
        else
        {
            return new VirtualAssistant.Core.Speech.TranscriptionResult(
                result.ErrorMessage ?? "Transcription failed"
            );
        }
    }

    public async Task<VirtualAssistant.Core.Speech.TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        return await TranscribeAsync(memoryStream.ToArray(), cancellationToken);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
```

## 6. Dependency Injection Changes

### Current Registration (VoiceServicesExtensions.cs)

**BEFORE** (lines 62-73):
```csharp
// Use SpeechToText gRPC microservice instead of local Whisper.net
services.AddSingleton<ISpeechTranscriber>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SpeechToTextGrpcClient>>();
    var dictationOptions = sp.GetRequiredService<IOptions<DictationOptions>>();

    return new SpeechToTextGrpcClient(
        logger,
        dictationOptions.Value.WhisperLanguage,
        dictationOptions.Value.WhisperModelPath);
});
```

### New Registration

**AFTER**:
```csharp
// Whisper.net configuration
services.Configure<WhisperOptions>(configuration.GetSection("Whisper"));

// Register WhisperNetProvider
services.AddSingleton<ITranscriptionProvider, WhisperNetProvider>();

// Register ISpeechTranscriber adapter
services.AddSingleton<ISpeechTranscriber>(sp =>
{
    var provider = sp.GetRequiredService<ITranscriptionProvider>();
    var dictationOptions = sp.GetRequiredService<IOptions<DictationOptions>>();

    return new WhisperNetTranscriber(
        provider,
        dictationOptions.Value.WhisperLanguage);
});
```

## 7. Configuration Changes

### New Configuration Section (appsettings.json)

Add to VirtualAssistant configuration:

```json
{
  "Whisper": {
    "ModelPath": "base",
    "DefaultLanguage": "cs",
    "UseGpu": true,
    "MaxConcurrentRequests": 3
  }
}
```

### Configuration Mapping

| SpeechToText.SpeechToTextOptions | VirtualAssistant.DictationOptions | Notes |
|----------------------------------|----------------------------------|-------|
| ModelPath | WhisperModelPath | Already exists |
| DefaultLanguage | WhisperLanguage | Already exists |
| UseGpu | N/A | New, add to WhisperOptions |
| MaxConcurrentRequests | N/A | New, add to WhisperOptions |

**Note**: WhisperModelLocator.cs will resolve model filename from ModelPath (e.g., "base" → "ggml-base.bin").

## 8. Shared Model Storage (FHS-Compliant)

Both VirtualAssistant and SpeechToText use the same model location logic (WhisperModelLocator):

**Model Search Order**:
1. User-specific: `~/.local/share/whisper-models/`
2. System-wide: `/usr/local/share/whisper-models/`
3. Legacy: `/home/jirka/Olbrasoft/VirtualAssistant/models/` (fallback)

**Current Models** (from CLAUDE.md):
- Shared location: `~/.local/share/whisper-models/` (5.9 GB)
- Shared with: PushToTalk (no longer used)

**Action**: No changes needed - WhisperModelLocator already implements FHS-compliant lookup.

## 9. Breaking Changes

### Removed Features

1. **gRPC Endpoint** - `http://localhost:5052` will no longer be available
   - Impact: None (PushToTalk no longer used)

2. **REST API Fallback** - `/api/stt/transcribe` endpoint removed
   - Impact: None (not used externally)

3. **Service Environment Variable** - `SPEECHTOTEXT_SERVICE_URL` no longer used
   - Impact: Remove from systemd service file

### Behavioral Changes

1. **Model Loading** - Models loaded into VRAM on first request (not at startup)
   - Current: gRPC service loads model at startup
   - New: WhisperNetProvider lazy-loads on first TranscribeAsync call
   - Impact: First transcription ~2-3s slower

2. **Concurrent Requests** - Limited by MaxConcurrentRequests (default: 3)
   - Current: gRPC handles concurrency internally
   - New: SemaphoreSlim in WhisperNetProvider
   - Impact: Same behavior, different implementation

3. **Error Handling** - No network errors (no gRPC channel failures)
   - Current: Can fail with "gRPC error: ..."
   - New: Only Whisper.net exceptions
   - Impact: More reliable (no network dependency)

## 10. Testing Strategy

### Unit Tests to Update

1. **WhisperNetProvider Tests** - Copy from SpeechToText.Tests
   - Test model caching
   - Test GPU initialization
   - Test concurrent request limiting

2. **WhisperNetTranscriber Tests** - New test class
   - Test adapter mapping (byte[] → TranscriptionRequest)
   - Test result mapping (SpeechToText.Result → VirtualAssistant.Result)
   - Test error handling

3. **Integration Tests** - Update existing tests
   - Replace gRPC mock with WhisperNetProvider mock
   - Test end-to-end transcription flow
   - Test model path resolution

### Manual Testing Checklist

- [ ] Dictation mode starts successfully
- [ ] Transcription accuracy matches previous gRPC version
- [ ] GPU acceleration works (check CUDA usage)
- [ ] Model caching works (2nd request faster than 1st)
- [ ] Concurrent requests handled correctly
- [ ] Error messages clear and actionable
- [ ] No regression in LLM correction pipeline

## 11. Deployment Checklist

### Pre-Deployment

- [ ] All unit tests pass
- [ ] Integration tests pass
- [ ] Code review completed
- [ ] Documentation updated

### Deployment Steps

1. Deploy VirtualAssistant with inline Whisper.net
2. Restart virtual-assistant.service
3. Verify service starts without errors
4. Test dictation functionality
5. Monitor logs for 5 minutes

### Post-Deployment

- [ ] Stop SpeechToText microservice (`systemctl --user stop speech-to-text.service`)
- [ ] Disable SpeechToText microservice (`systemctl --user disable speech-to-text.service`)
- [ ] Archive SpeechToText project (keep code, don't delete)

### Rollback Plan

If inline implementation fails:
1. Revert VirtualAssistant deployment
2. Start SpeechToText microservice
3. Restart virtual-assistant.service
4. Investigate logs and create bug issue

## 12. Performance Comparison

| Metric | gRPC Microservice | Inline Whisper.net | Notes |
|--------|-------------------|-------------------|-------|
| **First Request** | ~200ms + transcription | ~2-3s + transcription | Model load penalty (inline) |
| **Subsequent Requests** | ~200ms + transcription | ~transcription only | Model cached in VRAM |
| **Network Overhead** | ~50-100ms | 0ms | No gRPC serialization |
| **Memory Usage** | 2 processes | 1 process | Simplified architecture |
| **Startup Time** | Service: ~2s, Client: instant | ~2s (lazy load) | Model loaded on demand |

**Expected Improvement**: 50-100ms faster per request after first transcription (no network/serialization overhead).

## 13. File Migration Checklist

### Files to Copy

- [ ] WhisperModelLocator.cs
- [ ] SpeechToTextOptions.cs (rename to WhisperOptions.cs)
- [ ] ITranscriptionProvider.cs
- [ ] TranscriptionRequest.cs
- [ ] TranscriptionResult.cs (rename to SttTranscriptionResult.cs to avoid conflict)
- [ ] TranscriptionProviderInfo.cs
- [ ] WhisperNetProvider.cs

### Files to Create

- [ ] WhisperNetTranscriber.cs (adapter)

### Files to Delete

- [ ] SpeechToTextGrpcClient.cs
- [ ] speech_to_text.proto

### Files to Modify

- [ ] VirtualAssistant.Voice.csproj (add Whisper.net packages, remove gRPC)
- [ ] VoiceServicesExtensions.cs (replace gRPC registration)
- [ ] appsettings.json (add Whisper configuration)

## 14. Next Steps

1. **Issue #459** - Move Core Files (WhisperModelLocator, options, models)
2. **Issue #460** - Move WhisperNetProvider implementation
3. **Issue #461** - Replace gRPC Client (create WhisperNetTranscriber)
4. **Issue #462** - Update Configuration (appsettings.json, DI)
5. **Issue #463** - Update Tests (unit + integration)
6. **Issue #464** - Documentation (CLAUDE.md, README.md)
7. **Issue #465** - Cleanup (disable service, archive project)

## Appendix A: Current vs Target Architecture

### Current Architecture (Microservice)

```
┌─────────────────────────────────────┐
│   VirtualAssistant.Service :5055    │
│                                     │
│  ┌───────────────────────────────┐ │
│  │  TranscriptionService         │ │
│  │  (LLM correction + filtering) │ │
│  │                               │ │
│  │  ┌─────────────────────────┐  │ │
│  │  │ ISpeechTranscriber      │  │ │
│  │  │ (interface)             │  │ │
│  │  └────────┬────────────────┘  │ │
│  │           │                   │ │
│  │           ▼                   │ │
│  │  ┌─────────────────────────┐  │ │
│  │  │ SpeechToTextGrpcClient  │  │ │
│  │  │ (gRPC adapter)          │  │ │
│  │  └────────┬────────────────┘  │ │
│  └───────────┼───────────────────┘ │
│              │ gRPC call           │
│              │ localhost:5052      │
└──────────────┼─────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│   SpeechToText.Service :5052         │
│                                      │
│  ┌────────────────────────────────┐  │
│  │  SttGrpcService (gRPC server)  │  │
│  │                                │  │
│  │  ┌──────────────────────────┐  │  │
│  │  │ ITranscriptionProvider   │  │  │
│  │  │ (interface)              │  │  │
│  │  └───────┬──────────────────┘  │  │
│  │          ▼                     │  │
│  │  ┌──────────────────────────┐  │  │
│  │  │ WhisperNetProvider       │  │  │
│  │  │ (Whisper.net wrapper)    │  │  │
│  │  │ - Model caching          │  │  │
│  │  │ - GPU acceleration       │  │  │
│  │  │ - Semaphore locking      │  │  │
│  │  └──────────────────────────┘  │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘
```

### Target Architecture (Inline)

```
┌─────────────────────────────────────────────┐
│   VirtualAssistant.Service :5055            │
│                                             │
│  ┌───────────────────────────────────────┐  │
│  │  TranscriptionService                 │  │
│  │  (LLM correction + filtering)         │  │
│  │                                       │  │
│  │  ┌─────────────────────────────────┐  │  │
│  │  │ ISpeechTranscriber (interface)  │  │  │
│  │  └────────┬────────────────────────┘  │  │
│  │           ▼                           │  │
│  │  ┌─────────────────────────────────┐  │  │
│  │  │ WhisperNetTranscriber (adapter) │  │  │
│  │  │                                 │  │  │
│  │  │  ┌───────────────────────────┐  │  │  │
│  │  │  │ ITranscriptionProvider    │  │  │  │
│  │  │  │ (interface)               │  │  │  │
│  │  │  └────────┬──────────────────┘  │  │  │
│  │  │           ▼                     │  │  │
│  │  │  ┌───────────────────────────┐  │  │  │
│  │  │  │ WhisperNetProvider        │  │  │  │
│  │  │  │ (Whisper.net wrapper)     │  │  │  │
│  │  │  │ - Model caching           │  │  │  │
│  │  │  │ - GPU acceleration        │  │  │  │
│  │  │  │ - Semaphore locking       │  │  │  │
│  │  │  └───────────────────────────┘  │  │  │
│  │  └─────────────────────────────────┘  │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘

No SpeechToText.Service needed!
```

**Key Changes**:
- ✅ WhisperNetProvider moved into VirtualAssistant.Voice
- ✅ WhisperNetTranscriber adapts ITranscriptionProvider → ISpeechTranscriber
- ✅ No network communication (direct in-process calls)
- ✅ Simpler deployment (1 service instead of 2)
- ✅ Lower latency (no gRPC serialization overhead)

## Appendix B: Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **Model loading regression** | Medium | High | Test model caching thoroughly |
| **GPU initialization failure** | Low | High | Keep CUDA runtime dependency |
| **Memory leak in provider** | Low | Medium | Run stress tests with many requests |
| **Breaking VirtualAssistant** | Medium | High | Deploy to test environment first |
| **Configuration errors** | Medium | Medium | Validate config at startup |
| **Lost gRPC telemetry** | Low | Low | Add equivalent logging in provider |

---

**Document Status**: ✅ Complete
**Next Action**: Issue #459 - Move Core Files from SpeechToText.Core to VirtualAssistant.Voice
