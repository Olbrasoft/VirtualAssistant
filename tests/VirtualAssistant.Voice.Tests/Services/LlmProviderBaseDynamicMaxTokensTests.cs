using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for the dynamic max_tokens calculation and truncation
/// detection added to LlmProviderBase as part of issue #933.
/// </summary>
public class LlmProviderBaseDynamicMaxTokensTests
{
    [Fact]
    public void CalculateMaxTokens_ShortInput_ReturnsConfiguredFloor()
    {
        var provider = CreateProvider();

        // 60 chars ≈ 24 tokens, way below the floor of 1000
        var result = provider.PublicCalculateMaxTokens("hello world", configuredMin: 1000, reasoningBuffer: 0, maxAllowed: 16384);

        Assert.Equal(1000, result);
    }

    [Fact]
    public void CalculateMaxTokens_LongInput_ScalesUpFromFloor()
    {
        var provider = CreateProvider();

        // 1756-char input from the real truncation incident (voice_transcriptions.id=11577)
        // Estimated output: ceil(1756 / 2.5) = 703 tokens
        // With reasoning buffer 1500: 703 + 1500 = 2203 tokens
        // Above the 1000 floor → returns 2203
        var input = new string('x', 1756);
        var result = provider.PublicCalculateMaxTokens(input, configuredMin: 1000, reasoningBuffer: 1500, maxAllowed: 16384);

        Assert.Equal(2203, result);
    }

    [Fact]
    public void CalculateMaxTokens_VeryLongInput_ClampsToProviderCap()
    {
        var provider = CreateProvider();

        // 10000-char input → 4000 estimated output + 1500 reasoning = 5500 tokens
        // With cap of 4096 → returns 4096
        var input = new string('x', 10000);
        var result = provider.PublicCalculateMaxTokens(input, configuredMin: 1000, reasoningBuffer: 1500, maxAllowed: 4096);

        Assert.Equal(4096, result);
    }

    [Fact]
    public void CalculateMaxTokens_NoReasoningBuffer_OutputOnly()
    {
        var provider = CreateProvider();

        var input = new string('x', 1500);
        // ceil(1500 / 2.5) = 600 → below floor 1000 → returns 1000
        var result = provider.PublicCalculateMaxTokens(input, configuredMin: 1000, reasoningBuffer: 0, maxAllowed: 16384);

        Assert.Equal(1000, result);
    }

    [Fact]
    public void DetectTruncation_NormalCorrection_ReturnsFalse()
    {
        var provider = CreateProvider();
        var input = "Toto je test ceske vety pro overeni LLM korekce. Mela by se vratit cela.";
        var corrected = "Toto je test české věty pro ověření LLM korekce. Měla by se vrátit celá.";

        var truncated = provider.PublicDetectTruncation(input, corrected, completionTokens: 30, maxTokensSent: 1000);

        Assert.False(truncated);
    }

    [Fact]
    public void DetectTruncation_HitsMaxTokensCap_ReturnsTrue()
    {
        var provider = CreateProvider();
        var input = "Some input text that does not really matter for this assertion.";
        var corrected = "Output that goes on and on and on, ending without a period";

        // completion_tokens 950 / max_tokens 1000 = 95% → flagged
        var truncated = provider.PublicDetectTruncation(input, corrected, completionTokens: 950, maxTokensSent: 1000);

        Assert.True(truncated);
    }

    [Fact]
    public void DetectTruncation_RealIncident_11577_DetectsTruncation()
    {
        var provider = CreateProvider();
        // Reproducing voice_transcriptions.id=11577 from issue #933:
        // Whisper text was 1756 chars, LLM correction came back 845 chars
        // ending with "Teprve když ho stisknu, rozd" (mid-word).
        var input = new string('x', 1756);
        var corrected = new string('y', 844) + "rozd";  // ends mid-word

        // Even without completion_tokens info, the heuristics should fire:
        //   - corrected length 848 < 50% of input 1756 (878) → close
        //   - ends without terminal punctuation
        //   - last "word" is the entire 848-char string with no spaces → trailing word > 25
        var truncated = provider.PublicDetectTruncation(input, corrected, completionTokens: null, maxTokensSent: 1000);

        Assert.True(truncated);
    }

    [Fact]
    public void DetectTruncation_ShortInputWithoutPeriod_DoesNotFalseTrigger()
    {
        var provider = CreateProvider();
        // Below the 200-char threshold → reason 2 cannot fire.
        // Trailing word is short (< 25 chars) → reason 3 cannot fire.
        // No completion_tokens info → reason 1 cannot fire.
        var input = "Hello world";
        var corrected = "Ahoj svete";  // no period, but legitimate

        var truncated = provider.PublicDetectTruncation(input, corrected, completionTokens: null, maxTokensSent: 1000);

        Assert.False(truncated);
    }

    [Fact]
    public void DetectTruncation_EndsMidWord_LongTrailingWord_ReturnsTrue()
    {
        var provider = CreateProvider();
        // Trailing word is much longer than 25 chars and no terminal punctuation
        var input = "An input long enough to make the heuristic eligible.";
        var corrected = "An output that ends with averylongunbrokenrunofcharactersmuchlongerthan25";

        var truncated = provider.PublicDetectTruncation(input, corrected, completionTokens: null, maxTokensSent: 1000);

        Assert.True(truncated);
    }

    private static TestableLlmProvider CreateProvider()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost") };
        return new TestableLlmProvider(
            httpClient,
            Mock.Of<IPromptCache>(),
            Mock.Of<ILogger>(),
            Mock.Of<IDesktopContextService>(),
            Mock.Of<IQueryProcessor>(),
            Mock.Of<ICliAppDetector>(),
            Mock.Of<IServiceScopeFactory>());
    }

    /// <summary>
    /// Test-only subclass that exposes the protected methods on
    /// LlmProviderBase as public so the unit tests can call them directly.
    /// </summary>
    private class TestableLlmProvider : LlmProviderBase
    {
        public TestableLlmProvider(
            HttpClient httpClient,
            IPromptCache promptCache,
            ILogger logger,
            IDesktopContextService desktopContextService,
            IQueryProcessor queryProcessor,
            ICliAppDetector cliAppDetector,
            IServiceScopeFactory scopeFactory)
            : base(httpClient, promptCache, logger, desktopContextService, queryProcessor, cliAppDetector, scopeFactory, initialEnabled: true)
        {
        }

        public override string ProviderName => "test";
        public override string ModelName => "test-model";
        protected override bool ConfigEnabled => true;
        protected override int MinTextLength => 0;
        protected override string ChatCompletionsEndpoint => "test";
        protected override ILlmProviderOptions Options => new TestOptions();

        public int PublicCalculateMaxTokens(string text, int configuredMin, int reasoningBuffer, int maxAllowed)
            => CalculateMaxTokens(text, configuredMin, reasoningBuffer, maxAllowed);

        public bool PublicDetectTruncation(string inputText, string correctedText, int? completionTokens, int maxTokensSent)
            => DetectTruncation(inputText, correctedText, completionTokens, maxTokensSent);

        public override Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public override Task<LlmCorrectionResult> CorrectTextAsync(string text, string promptText, int promptId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        private class TestOptions : ILlmProviderOptions
        {
            public string ApiKey => "test";
            public string BaseUrl => "http://localhost";
            public string Model => "test";
            public int TimeoutSeconds => 30;
            public int MaxTokens => 1000;
            public double Temperature => 0.5;
            public int MinTextLengthForCorrection => 0;
            public bool Enabled => true;
        }
    }
}
