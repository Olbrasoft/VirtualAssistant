using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Owns the "save a completed dictation session to the database" side-quest
/// that used to live inline on <c>DictationWorker</c>. Encapsulates:
/// <list type="bullet">
///   <item>constructing the <c>LlmCorrectionResult</c> from the optional
///         LLM-correction metadata on <see cref="TranscriptionResult"/>;</item>
///   <item>applying the Whisper (id = 13) fallback when the active transcriber
///         hasn't set <c>SttProviderId</c>;</item>
///   <item>dispatching between the racing-aware and single-provider persistence
///         overloads based on <c>RaceGroupId</c>;</item>
///   <item>creating and disposing the scoped DI scope for the scoped
///         <c>IDictationPersistenceService</c>.</item>
/// </list>
/// Lifts <c>IServiceScopeFactory</c> out of the worker's ctor (#969 split).
/// </summary>
public interface IDictationTranscriptionPersister
{
    /// <summary>
    /// Persists the completed transcription, honoring racing metadata when
    /// present. Propagates cancellation via <paramref name="cancellationToken"/>.
    /// </summary>
    Task SaveAsync(byte[] audioData, TranscriptionResult result, CancellationToken cancellationToken);
}
