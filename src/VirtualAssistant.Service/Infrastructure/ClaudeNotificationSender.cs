using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Sends TTS notifications for Claude execution events.
/// </summary>
public class ClaudeNotificationSender : IClaudeNotificationSender
{
    private readonly ILogger<ClaudeNotificationSender> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClaudeDispatchOptions _options;

    public ClaudeNotificationSender(
        ILogger<ClaudeNotificationSender> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ClaudeDispatchOptions> options)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task NotifyErrorAsync(string message)
    {
        await SendNotificationAsync(message);
    }

    /// <inheritdoc />
    public async Task NotifySuccessAsync(string message)
    {
        if (_options.NotifyOnSuccess)
        {
            await SendNotificationAsync(message);
        }
    }

    private async Task SendNotificationAsync(string message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(
                JsonSerializer.Serialize(new { text = message, source = "claude" }),
                Encoding.UTF8,
                "application/json");

            await client.PostAsync(_options.NotifyUrl, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send TTS notification");
        }
    }
}
