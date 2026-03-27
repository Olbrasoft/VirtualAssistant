using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;
using Olbrasoft.VirtualAssistant.Data.Commands.VoiceTranscriptionCommands;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Service for persisting dictation transcriptions and LLM corrections to database.
/// Implements Single Responsibility Principle - only handles database persistence.
/// </summary>
public class DictationPersistenceService : IDictationPersistenceService
{
    private readonly ILogger<DictationPersistenceService> _logger;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioRecordingOptions _options;

    public DictationPersistenceService(
        ILogger<DictationPersistenceService> logger,
        ICommandExecutor commandExecutor,
        IServiceScopeFactory scopeFactory,
        IOptions<AudioRecordingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<int?> SaveTranscriptionAsync(
        byte[] audioData,
        string originalText,
        LlmCorrectionResult? correctionResult,
        int sttProviderId,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        if (audioData is null || audioData.Length == 0)
        {
            throw new ArgumentException("Audio data cannot be null or empty.", nameof(audioData));
        }

        if (string.IsNullOrWhiteSpace(originalText))
        {
            throw new ArgumentException("Original text cannot be null or empty.", nameof(originalText));
        }

        // Validate audio data length (must be even for 16-bit samples)
        if (audioData.Length % _options.BytesPerSample != 0)
        {
            _logger.LogWarning(
                "Audio data length {Length} is not divisible by {BytesPerSample} (16-bit samples). Truncating to nearest even length.",
                audioData.Length,
                _options.BytesPerSample);
        }

        // Calculate audio duration from audio data (16-bit mono @ 16kHz)
        // duration_ms = (bytes / bytes_per_sample) / sample_rate * ms_per_second
        var sampleCount = audioData.Length / _options.BytesPerSample;
        var audioDurationMs = (int)((double)sampleCount / _options.SampleRate * AudioRecordingOptions.MillisecondsPerSecond);

        // Save original voice transcription (before LLM correction)
        VoiceTranscription transcription;
        try
        {
            var command = new SaveVoiceTranscriptionCommand(
                Text: originalText,
                DurationMs: audioDurationMs,
                ProviderId: sttProviderId
            );
            transcription = await _commandExecutor.ExecuteAsync(command, cancellationToken);

            _logger.LogDebug("Saved voice transcription to database with ID {Id}", transcription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save voice transcription to database");
            return null; // Cannot continue without transcription ID
        }

        // If LLM correction was applied, save it to database
        // Note: If LLM save fails, we still return the transcription ID (graceful degradation)
        if (correctionResult != null && correctionResult.CorrectedText != originalText)
        {
            // ModelId is required for all corrections
            if (!correctionResult.ModelId.HasValue)
            {
                _logger.LogError("Cannot save LLM correction without ModelId for transcription {TranscriptionId}. " +
                    "This indicates a bug in the LLM provider - it should always return ModelId when correction is applied.",
                    transcription.Id);
                return transcription.Id;
            }

            try
            {
                var command = new SaveLlmCorrectionCommand(
                    VoiceTranscriptionId: transcription.Id,
                    CorrectedText: correctionResult.CorrectedText,
                    DurationMs: correctionResult.DurationMs,
                    PromptId: correctionResult.PromptId,
                    ModelId: correctionResult.ModelId.Value,  // Required - every correction must have a model
                    InputTokens: correctionResult.InputTokens,
                    OutputTokens: correctionResult.OutputTokens,
                    ReasoningTokens: correctionResult.ReasoningTokens
                );
                var correction = await _commandExecutor.ExecuteAsync(command, cancellationToken);

                _logger.LogDebug(
                    "Saved LLM correction {Id} for transcription {TranscriptionId} using prompt ID {PromptId}, model ID {ModelId} (duration: {Duration}ms): '{Original}' → '{Corrected}'",
                    correction.Id,
                    transcription.Id,
                    correctionResult.PromptId,
                    correctionResult.ModelId,
                    correctionResult.DurationMs,
                    originalText.Length > 30 ? originalText[..30] + "..." : originalText,
                    correctionResult.CorrectedText.Length > 30 ? correctionResult.CorrectedText[..30] + "..." : correctionResult.CorrectedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save LLM correction for transcription {TranscriptionId}. Continuing with transcription ID.", transcription.Id);
                // Return transcription ID anyway - LLM correction is optional
            }
        }

        return transcription.Id;
    }

    /// <inheritdoc />
    public async Task<int?> SaveTranscriptionWithRacingAsync(
        byte[] audioData,
        string originalText,
        LlmCorrectionResult? correctionResult,
        int sttProviderId,
        Guid raceGroupId,
        Task<LlmCorrectionResult?>? loserTask,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        if (audioData is null || audioData.Length == 0)
            throw new ArgumentException("Audio data cannot be null or empty.", nameof(audioData));

        if (string.IsNullOrWhiteSpace(originalText))
            throw new ArgumentException("Original text cannot be null or empty.", nameof(originalText));

        if (audioData.Length % _options.BytesPerSample != 0)
        {
            _logger.LogWarning("Audio data length {Length} is not divisible by {BytesPerSample}.",
                audioData.Length, _options.BytesPerSample);
        }

        var sampleCount = audioData.Length / _options.BytesPerSample;
        var audioDurationMs = (int)((double)sampleCount / _options.SampleRate * AudioRecordingOptions.MillisecondsPerSecond);

        // Save original voice transcription
        VoiceTranscription transcription;
        try
        {
            var command = new SaveVoiceTranscriptionCommand(
                Text: originalText,
                DurationMs: audioDurationMs,
                ProviderId: sttProviderId
            );
            transcription = await _commandExecutor.ExecuteAsync(command, cancellationToken);
            _logger.LogDebug("Saved voice transcription to database with ID {Id}", transcription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save voice transcription to database");
            return null;
        }

        // Save winner LLM correction
        if (correctionResult != null && correctionResult.CorrectedText != originalText)
        {
            if (!correctionResult.ModelId.HasValue)
            {
                _logger.LogError("Cannot save LLM correction without ModelId for transcription {TranscriptionId}.", transcription.Id);
                return transcription.Id;
            }

            try
            {
                var command = new SaveLlmCorrectionCommand(
                    VoiceTranscriptionId: transcription.Id,
                    CorrectedText: correctionResult.CorrectedText,
                    DurationMs: correctionResult.DurationMs,
                    PromptId: correctionResult.PromptId,
                    ModelId: correctionResult.ModelId.Value,
                    InputTokens: correctionResult.InputTokens,
                    OutputTokens: correctionResult.OutputTokens,
                    ReasoningTokens: correctionResult.ReasoningTokens,
                    IsWinner: true,
                    RaceGroupId: raceGroupId
                );
                await _commandExecutor.ExecuteAsync(command, cancellationToken);
                _logger.LogDebug("Saved racing winner correction for transcription {TranscriptionId} (RaceGroupId: {RaceGroupId})",
                    transcription.Id, raceGroupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save winner LLM correction for transcription {TranscriptionId}", transcription.Id);
            }
        }

        // Fire-and-forget: save loser result when it arrives
        if (loserTask != null)
        {
            var transcriptionId = transcription.Id;
            _ = Task.Run(async () =>
            {
                try
                {
                    var loserResult = await loserTask;
                    if (loserResult == null || !loserResult.ModelId.HasValue)
                    {
                        _logger.LogDebug("Racing loser returned no result for transcription {TranscriptionId}", transcriptionId);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var commandExecutor = scope.ServiceProvider.GetRequiredService<ICommandExecutor>();

                    var command = new SaveLlmCorrectionCommand(
                        VoiceTranscriptionId: transcriptionId,
                        CorrectedText: loserResult.CorrectedText,
                        DurationMs: loserResult.DurationMs,
                        PromptId: loserResult.PromptId,
                        ModelId: loserResult.ModelId.Value,
                        InputTokens: loserResult.InputTokens,
                        OutputTokens: loserResult.OutputTokens,
                        ReasoningTokens: loserResult.ReasoningTokens,
                        IsWinner: false,
                        RaceGroupId: raceGroupId
                    );
                    await commandExecutor.ExecuteAsync(command, cancellationToken);

                    _logger.LogInformation("Saved racing loser correction in {Duration}ms for transcription {TranscriptionId} (RaceGroupId: {RaceGroupId})",
                        loserResult.DurationMs, transcriptionId, raceGroupId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Racing loser save cancelled for transcription {TranscriptionId}", transcriptionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save racing loser correction for transcription {TranscriptionId}", transcriptionId);
                }
            }, cancellationToken);
        }

        return transcription.Id;
    }
}
