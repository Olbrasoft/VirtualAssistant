using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VirtualAssistant.Data.Dtos.Clipboard;
using VirtualAssistant.Data.Dtos.Common;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for clipboard operations.
/// Provides endpoints for interacting with system clipboard.
/// </summary>
[ApiController]
[Route("api/hub")]
[Produces("application/json")]
public class ClipboardController : ControllerBase
{
    private readonly ILogger<ClipboardController> _logger;

    /// <summary>
    /// Initializes a new instance of the ClipboardController.
    /// </summary>
    /// <param name="logger">The logger</param>
    public ClipboardController(ILogger<ClipboardController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Copy content to system clipboard using xclip.
    /// </summary>
    /// <param name="request">Clipboard request with content</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Content copied to clipboard</response>
    /// <response code="400">Invalid request or clipboard operation failed</response>
    [HttpPost("clipboard")]
    [ProducesResponseType(typeof(ClipboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClipboardResponse>> ToClipboard(
        [FromBody] ClipboardRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Content))
        {
            return BadRequest(new ErrorResponse { Error = "Content cannot be empty" });
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xclip",
                    Arguments = "-selection clipboard",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.StandardInput.WriteAsync(request.Content);
            process.StandardInput.Close();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                _logger.LogWarning("xclip failed with exit code {Code}: {Error}", process.ExitCode, error);
                return BadRequest(new ErrorResponse { Error = $"Clipboard operation failed: {error}" });
            }

            _logger.LogInformation("Content copied to clipboard ({Length} chars)", request.Content.Length);

            return Ok(new ClipboardResponse
            {
                Success = true,
                Message = "Content copied to clipboard"
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to copy to clipboard");
            return BadRequest(new ErrorResponse { Error = $"Clipboard operation failed: {ex.Message}" });
        }
    }
}
