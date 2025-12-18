using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// Controller for handling GitHub webhook events.
/// Supports automatic deployment on push events.
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class GitHubWebhooksController : ControllerBase
{
    private readonly ILogger<GitHubWebhooksController> _logger;
    private readonly IConfiguration _configuration;

    // Map repository names to deploy scripts
    private static readonly Dictionary<string, string> DeployScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PushToTalk"] = "/home/jirka/Olbrasoft/PushToTalk/deploy/deploy.sh",
        ["VirtualAssistant"] = "/home/jirka/Olbrasoft/VirtualAssistant/deploy/deploy.sh"
    };

    public GitHubWebhooksController(
        ILogger<GitHubWebhooksController> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Receives GitHub webhook events.
    /// </summary>
    [HttpPost("github")]
    public async Task<IActionResult> HandleGitHubWebhook()
    {
        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault();
        var delivery = Request.Headers["X-GitHub-Delivery"].FirstOrDefault();
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

        _logger.LogInformation("Received GitHub webhook: Event={Event}, Delivery={Delivery}",
            eventType, delivery);

        // Read body
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        // Verify signature if secret is configured
        var webhookSecret = _configuration["GitHub:WebhookSecret"];
        if (!string.IsNullOrEmpty(webhookSecret) && !string.IsNullOrEmpty(signature))
        {
            if (!VerifySignature(body, signature, webhookSecret))
            {
                _logger.LogWarning("Invalid webhook signature for delivery {Delivery}", delivery);
                return Unauthorized("Invalid signature");
            }
        }

        // Handle different event types
        return eventType switch
        {
            "ping" => HandlePing(body),
            "push" => await HandlePushAsync(body),
            _ => Ok(new { message = $"Event '{eventType}' acknowledged but not processed" })
        };
    }

    private IActionResult HandlePing(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var zen = doc.RootElement.TryGetProperty("zen", out var zenProp)
                ? zenProp.GetString()
                : "No zen";

            _logger.LogInformation("GitHub ping received: {Zen}", zen);
            return Ok(new { message = "pong", zen });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse ping payload");
            return Ok(new { message = "pong" });
        }
    }

    private async Task<IActionResult> HandlePushAsync(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Extract repository info
            var repoName = root.TryGetProperty("repository", out var repoProp)
                && repoProp.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : null;

            var refName = root.TryGetProperty("ref", out var refProp)
                ? refProp.GetString()
                : null;

            var pusher = root.TryGetProperty("pusher", out var pusherProp)
                && pusherProp.TryGetProperty("name", out var pusherNameProp)
                ? pusherNameProp.GetString()
                : "unknown";

            _logger.LogInformation("Push event: Repo={Repo}, Ref={Ref}, Pusher={Pusher}",
                repoName, refName, pusher);

            // Only deploy on push to main/master branch
            if (refName != "refs/heads/main" && refName != "refs/heads/master")
            {
                _logger.LogInformation("Ignoring push to non-main branch: {Ref}", refName);
                return Ok(new { message = "Push acknowledged", deployed = false, reason = "Not main branch" });
            }

            // Check if we have a deploy script for this repo
            if (string.IsNullOrEmpty(repoName) || !DeployScripts.TryGetValue(repoName, out var deployScript))
            {
                _logger.LogInformation("No deploy script configured for repository: {Repo}", repoName);
                return Ok(new { message = "Push acknowledged", deployed = false, reason = "No deploy script" });
            }

            // Check if deploy script exists
            if (!System.IO.File.Exists(deployScript))
            {
                _logger.LogWarning("Deploy script not found: {Script}", deployScript);
                return Ok(new { message = "Push acknowledged", deployed = false, reason = "Deploy script not found" });
            }

            // First pull latest changes
            var repoPath = Path.GetDirectoryName(Path.GetDirectoryName(deployScript));
            _logger.LogInformation("Pulling latest changes from {RepoPath}", repoPath);

            var pullProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "pull origin main",
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            pullProcess.Start();
            var pullOutput = await pullProcess.StandardOutput.ReadToEndAsync();
            var pullError = await pullProcess.StandardError.ReadToEndAsync();
            await pullProcess.WaitForExitAsync();

            if (pullProcess.ExitCode != 0)
            {
                _logger.LogWarning("Git pull failed: {Error}", pullError);
                // Continue anyway - maybe it's already up to date
            }
            else
            {
                _logger.LogInformation("Git pull completed: {Output}", pullOutput.Trim());
            }

            // Run deploy script in background
            _logger.LogInformation("Starting deployment for {Repo} using {Script}", repoName, deployScript);

            _ = Task.Run(async () =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            // Pass --no-version-bump to avoid infinite webhook loop
                            Arguments = $"{deployScript} --no-version-bump",
                            WorkingDirectory = repoPath,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Deployment completed successfully for {Repo}", repoName);
                    }
                    else
                    {
                        _logger.LogError("Deployment failed for {Repo}: {Error}", repoName, error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during deployment for {Repo}", repoName);
                }
            });

            return Ok(new
            {
                message = "Deployment started",
                deployed = true,
                repository = repoName,
                @ref = refName
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse push payload");
            return BadRequest(new { error = "Invalid JSON payload" });
        }
    }

    private static bool VerifySignature(string payload, string signature, string secret)
    {
        if (!signature.StartsWith("sha256="))
            return false;

        var expectedSignature = signature[7..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var actualSignature = Convert.ToHexString(hash).ToLowerInvariant();

        return string.Equals(expectedSignature, actualSignature, StringComparison.OrdinalIgnoreCase);
    }
}
