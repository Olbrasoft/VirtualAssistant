using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class GoogleSpeechTranscriberTests
{
    private readonly Mock<ILogger<GoogleSpeechTranscriber>> _loggerMock;
    private readonly GoogleSpeechToTextOptions _options;

    public GoogleSpeechTranscriberTests()
    {
        _loggerMock = new Mock<ILogger<GoogleSpeechTranscriber>>();
        _options = new GoogleSpeechToTextOptions
        {
            ApiKey = "test-api-key",
            Language = "cs-CZ",
            TimeoutMs = 5000,
            Enabled = true
        };
    }

    private GoogleSpeechTranscriber CreateTranscriber(HttpClient httpClient)
    {
        return new GoogleSpeechTranscriber(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }

    [Fact]
    public void Language_ReturnsConfiguredLanguage()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var transcriber = CreateTranscriber(httpClient);

        // Act
        var language = transcriber.Language;

        // Assert
        Assert.Equal("cs-CZ", language);
    }

    [Fact]
    public async Task TranscribeAsync_WithEmptyAudio_ReturnsError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var transcriber = CreateTranscriber(httpClient);

        // Act
        var result = await transcriber.TranscribeAsync(Array.Empty<byte>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Audio data cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithNullAudio_ReturnsError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var transcriber = CreateTranscriber(httpClient);

        // Act
        var result = await transcriber.TranscribeAsync((byte[])null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Audio data cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithValidResponse_ReturnsTranscription()
    {
        // Arrange
        var googleResponse = """
            {"result":[]}
            {"result":[{"alternative":[{"transcript":"Ahoj světe","confidence":0.95}],"final":true}],"result_index":0}
            """;
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, googleResponse);
        var transcriber = CreateTranscriber(httpClient);
        var audioData = new byte[100]; // Dummy audio data

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Ahoj světe", result.Text);
        Assert.Equal(0.95f, result.Confidence);
    }

    [Fact]
    public async Task TranscribeAsync_WithNoConfidence_UsesDefaultConfidence()
    {
        // Arrange
        var googleResponse = """
            {"result":[{"alternative":[{"transcript":"Test"}],"final":true}],"result_index":0}
            """;
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, googleResponse);
        var transcriber = CreateTranscriber(httpClient);
        var audioData = new byte[100];

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Test", result.Text);
        Assert.Equal(0.9f, result.Confidence); // Default confidence
    }

    [Fact]
    public async Task TranscribeAsync_WithEmptyResults_ReturnsNoSpeechDetected()
    {
        // Arrange
        var googleResponse = """{"result":[]}""";
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, googleResponse);
        var transcriber = CreateTranscriber(httpClient);
        var audioData = new byte[100];

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No speech detected", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithHttpError_ReturnsError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "Server error");
        var transcriber = CreateTranscriber(httpClient);
        var audioData = new byte[100];

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("HTTP error", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithCancellation_ReturnsCancelled()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Cancelled", null, new CancellationToken(true)));

        var httpClient = new HttpClient(handlerMock.Object);
        var transcriber = CreateTranscriber(httpClient);
        var audioData = new byte[100];
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await transcriber.TranscribeAsync(audioData, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Transcription cancelled", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithStream_ConvertsToByteArrayAndTranscribes()
    {
        // Arrange
        var googleResponse = """
            {"result":[{"alternative":[{"transcript":"Stream test","confidence":0.92}],"final":true}],"result_index":0}
            """;
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, googleResponse);
        var transcriber = CreateTranscriber(httpClient);
        using var stream = new MemoryStream(new byte[100]);

        // Act
        var result = await transcriber.TranscribeAsync(stream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Stream test", result.Text);
    }

    [Fact]
    public async Task TranscribeAsync_WithNullStream_ReturnsError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var transcriber = CreateTranscriber(httpClient);

        // Act
        var result = await transcriber.TranscribeAsync((Stream)null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Audio stream cannot be null", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithWavHeader_StripsHeader()
    {
        // Arrange
        var googleResponse = """
            {"result":[{"alternative":[{"transcript":"WAV test","confidence":0.88}],"final":true}],"result_index":0}
            """;

        var handlerMock = new Mock<HttpMessageHandler>();
        byte[]? capturedContent = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedContent = await req.Content!.ReadAsByteArrayAsync(ct);
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(googleResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var transcriber = CreateTranscriber(httpClient);

        // Create WAV header + PCM data
        var wavData = new byte[100];
        wavData[0] = (byte)'R';
        wavData[1] = (byte)'I';
        wavData[2] = (byte)'F';
        wavData[3] = (byte)'F';
        // Fill rest with dummy data

        // Act
        var result = await transcriber.TranscribeAsync(wavData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("WAV test", result.Text);
        Assert.NotNull(capturedContent);
        Assert.Equal(56, capturedContent.Length); // 100 - 44 (WAV header)
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var transcriber = CreateTranscriber(httpClient);

        // Act & Assert - should not throw
        transcriber.Dispose();
        transcriber.Dispose();
    }

    [Fact]
    public async Task TranscribeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var transcriber = CreateTranscriber(httpClient);
        transcriber.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            transcriber.TranscribeAsync(new byte[100]));
    }
}
