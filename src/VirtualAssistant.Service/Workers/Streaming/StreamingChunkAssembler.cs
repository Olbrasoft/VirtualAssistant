using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Streaming;

/// <inheritdoc />
public class StreamingChunkAssembler : IStreamingChunkAssembler
{
    private static readonly Regex CollapseWhitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly ILogger<StreamingChunkAssembler> _logger;
    private readonly ITranscriptionService _transcriptionService;

    private readonly ConcurrentDictionary<int, string> _results = new();
    private readonly ConcurrentDictionary<int, Task> _tasks = new();
    private CancellationTokenSource? _cts;
    private volatile bool _active;

    public StreamingChunkAssembler(
        ILogger<StreamingChunkAssembler> logger,
        ITranscriptionService transcriptionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
    }

    public bool IsActive => _active;

    public int CompletedChunkCount => _results.Count;

    public void Reset(bool active)
    {
        CancelAndClear();
        if (active)
        {
            // Publish the fresh CTS before flipping _active so any submitter that
            // observes _active==true via the volatile read below always sees a
            // non-null _cts.
            _cts = new CancellationTokenSource();
        }
        _active = active;
    }

    public void CancelAndClear()
    {
        // Flip inactive first so concurrent SubmitChunk callers bail out before
        // we dispose the CTS.
        _active = false;

        // Atomically swap the CTS out so only one caller disposes it even if
        // Reset/CancelAndClear race.
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts != null)
        {
            try { cts.Cancel(); } catch { /* best effort — handlers may have raced */ }
            cts.Dispose();
        }

        _results.Clear();
        _tasks.Clear();
    }

    public void SubmitChunk(int index, byte[] pcmBytes)
    {
        if (!_active) return;

        // Snapshot _cts so a racing CancelAndClear can't null/dispose it out
        // from under us between the null-check and the Token read. An
        // ObjectDisposedException is still possible if Dispose has already
        // completed on the snapshot — swallow it and treat as inactive.
        // (Copilot review on PR #1021.)
        var cts = _cts;
        if (cts == null) return;

        CancellationToken ct;
        try
        {
            if (cts.IsCancellationRequested) return;
            ct = cts.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        var task = Task.Run(async () =>
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var text = await _transcriptionService.TranscribeChunkRawAsync(pcmBytes, ct);
                sw.Stop();
                _results[index] = text ?? string.Empty;
                _logger.LogInformation(
                    "Streaming chunk {Index} transcribed in {ElapsedMs}ms: '{Preview}'",
                    index,
                    sw.ElapsedMilliseconds,
                    (text ?? string.Empty).Length > 60 ? (text ?? string.Empty)[..60] + "…" : text);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Streaming chunk {Index} canceled", index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Streaming chunk {Index} transcription failed", index);
                _results[index] = string.Empty;
            }
        }, ct);

        _tasks[index] = task;
    }

    public async Task<string?> CombineAsync(CancellationToken cancellationToken)
    {
        if (!_active) return null;

        var pending = _tasks.Values.ToArray();
        if (pending.Length == 0)
        {
            _logger.LogInformation("StreamingChunkAssembler.CombineAsync: no chunks produced");
            return null;
        }

        try
        {
            await Task.WhenAll(pending).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "One or more streaming chunk tasks faulted; proceeding with available results");
        }

        var ordered = _results
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0);

        var combined = string.Join(" ", ordered);
        return CollapseWhitespace.Replace(combined, " ").Trim();
    }
}
