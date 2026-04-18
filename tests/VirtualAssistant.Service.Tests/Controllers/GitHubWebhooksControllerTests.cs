using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Controllers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Controllers;

/// <summary>
/// Unit tests for GitHubWebhooksController.
/// Tests cover webhook handling, signature verification, and event processing.
/// </summary>
public class GitHubWebhooksControllerTests
{
    private const string TestSecret = "test-secret";

    private readonly Mock<ILogger<GitHubWebhooksController>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly GitHubWebhooksController _controller;

    public GitHubWebhooksControllerTests()
    {
        _loggerMock = new Mock<ILogger<GitHubWebhooksController>>();
        _configurationMock = new Mock<IConfiguration>();
        // Default: secret configured so happy-path tests that pass a valid signature work;
        // tests exercising the no-secret branch override this setup explicitly.
        _configurationMock.Setup(x => x["GitHub:WebhookSecret"]).Returns(TestSecret);

        _controller = new GitHubWebhooksController(
            _loggerMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task HandleGitHubWebhook_PingEvent_ReturnsPong()
    {
        // Arrange
        var pingPayload = @"{""zen"": ""Anything added dilutes everything else.""}";
        SetupControllerContext("ping", "delivery-123", ComputeSignature(pingPayload, TestSecret), pingPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task HandleGitHubWebhook_PushToNonMainBranch_ReturnsNotDeployed()
    {
        // Arrange
        var pushPayload = @"{
            ""ref"": ""refs/heads/feature-branch"",
            ""repository"": { ""name"": ""VirtualAssistant"" },
            ""pusher"": { ""name"": ""test-user"" }
        }";
        SetupControllerContext("push", "delivery-456", ComputeSignature(pushPayload, TestSecret), pushPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        // Controller returns anonymous object, check JSON representation for expected content
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("Not main branch", json);
    }

    [Fact]
    public async Task HandleGitHubWebhook_UnknownEventType_ReturnsAcknowledged()
    {
        // Arrange
        var unknownPayload = @"{}";
        SetupControllerContext("unknown_event", "delivery-789", ComputeSignature(unknownPayload, TestSecret), unknownPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        // Controller returns anonymous object, check JSON representation for expected content
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("acknowledged", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleGitHubWebhook_WithInvalidSignature_ReturnsUnauthorized()
    {
        // Arrange
        var pingPayload = @"{""zen"": ""Test""}";
        SetupControllerContext("ping", "delivery-000", "sha256=invalid_signature", pingPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid signature", unauthorizedResult.Value);
    }

    [Fact]
    public async Task HandleGitHubWebhook_WithValidSignature_ProcessesEvent()
    {
        // Arrange
        var pingPayload = @"{""zen"": ""Test""}";
        SetupControllerContext("ping", "delivery-001", ComputeSignature(pingPayload, TestSecret), pingPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task HandleGitHubWebhook_WithoutConfiguredSecret_ReturnsUnauthorized()
    {
        // Arrange — no secret configured (fail-closed under #978)
        _configurationMock.Setup(x => x["GitHub:WebhookSecret"]).Returns((string?)null);
        var controller = new GitHubWebhooksController(_loggerMock.Object, _configurationMock.Object);

        var pingPayload = @"{""zen"": ""Test""}";
        SetupControllerContext("ping", "delivery-no-secret", "sha256=any", pingPayload, controller);

        // Act
        var result = await controller.HandleGitHubWebhook();

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Webhook secret not configured", unauthorized.Value);
    }

    [Fact]
    public async Task HandleGitHubWebhook_WithoutSignatureHeader_ReturnsUnauthorized()
    {
        // Arrange — secret is configured, but the caller omitted the signature header
        var pingPayload = @"{""zen"": ""Test""}";
        SetupControllerContext("ping", "delivery-no-sig", signature: null, body: pingPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Missing signature", unauthorized.Value);
    }

    [Fact]
    public async Task HandleGitHubWebhook_PushToUnknownRepo_ReturnsNoDeployScript()
    {
        // Arrange
        var pushPayload = @"{
            ""ref"": ""refs/heads/main"",
            ""repository"": { ""name"": ""UnknownRepo"" },
            ""pusher"": { ""name"": ""test-user"" }
        }";
        SetupControllerContext("push", "delivery-unknown", ComputeSignature(pushPayload, TestSecret), pushPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        // Controller returns anonymous object, check JSON representation for expected content
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("No deploy script", json);
    }

    [Fact]
    public async Task HandleGitHubWebhook_PingWithMalformedJson_ReturnsOk()
    {
        // Arrange - malformed JSON should still return pong
        var malformedPayload = @"not valid json";
        SetupControllerContext("ping", "delivery-bad", ComputeSignature(malformedPayload, TestSecret), malformedPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task HandleGitHubWebhook_PushWithMalformedJson_ReturnsBadRequest()
    {
        // Arrange
        var malformedPayload = @"not valid json";
        SetupControllerContext("push", "delivery-bad-push", ComputeSignature(malformedPayload, TestSecret), malformedPayload);

        // Act
        var result = await _controller.HandleGitHubWebhook();

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badResult.Value);
    }

    private void SetupControllerContext(string eventType, string delivery, string? signature, string body, ControllerBase? targetController = null)
    {
        var controller = targetController ?? _controller;
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Headers["X-GitHub-Event"] = eventType;
        httpContext.Request.Headers["X-GitHub-Delivery"] = delivery;

        if (signature != null)
        {
            httpContext.Request.Headers["X-Hub-Signature-256"] = signature;
        }

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentLength = bodyBytes.Length;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
