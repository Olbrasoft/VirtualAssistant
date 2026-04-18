using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Services;

/// <summary>
/// Cases mirror the real transcriptions from <c>voice_transcriptions</c> that
/// prompted this trimmer — users dictating a prompt and closing with "Děkuji."
/// out of habit, plus a few classic Whisper hallucinations during silent tails.
/// The substring-safety tests pin down what must NEVER be touched so pasting
/// code/prose isn't silently mangled.
/// </summary>
public class CivilityTrimmerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void StripTrailingCivility_NullEmptyOrBlank_ReturnsSafely(string? input, string expected)
    {
        Assert.Equal(expected, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Theory]
    [InlineData("Děkuji.", "")]
    [InlineData("Děkuju.", "")]
    [InlineData("Díky.", "")]
    [InlineData("Ahoj.", "")]
    [InlineData("Čau.", "")]
    [InlineData("Díky moc.", "")]
    [InlineData("Díky za info.", "")]
    [InlineData("Děkuji za titulky.", "")]
    [InlineData("Děkuji za sledování.", "")]
    public void StripTrailingCivility_JustCivility_ReturnsEmpty(string input, string expected)
    {
        Assert.Equal(expected, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_CivilityFollowingContent_StripsOnlyTrailing()
    {
        // Real transcription from voice_transcriptions table, trimmed.
        var input = "Stáhneme video a nahrajeme ho na StreamTape. Děkuji.";
        var expected = "Stáhneme video a nahrajeme ho na StreamTape.";

        Assert.Equal(expected, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_MultipleChainedCivilities_StripsAll()
    {
        // Not something the user explicitly asked for but trivially correct if
        // the single-strip logic is applied iteratively — keeps us from having
        // to handle "Děkuji. Ahoj." awkwardly if Whisper ever produces it.
        var input = "Udělej to prosím. Děkuji. Ahoj.";
        var expected = "Udělej to prosím.";

        Assert.Equal(expected, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_CaseInsensitive_Strips()
    {
        // Whisper is usually consistent with casing but we shouldn't depend on it.
        Assert.Equal("Hotovo.", CivilityTrimmer.StripTrailingCivility("Hotovo. DĚKUJI."));
    }

    [Fact]
    public void StripTrailingCivility_ExclamationTerminator_Strips()
    {
        Assert.Equal("Hotovo.", CivilityTrimmer.StripTrailingCivility("Hotovo. Ahoj!"));
    }

    [Fact]
    public void StripTrailingCivility_EllipsisTerminator_Strips()
    {
        Assert.Equal("Hotovo.", CivilityTrimmer.StripTrailingCivility("Hotovo. Ahoj..."));
    }

    [Fact]
    public void StripTrailingCivility_NoTrailingCivility_ReturnsUnchanged()
    {
        // Real transcription — should pass through untouched.
        var input = "Prosím tě, dej mi informaci, ať můžu pokračovat.";

        Assert.Equal(input, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Theory]
    [InlineData("Hotovo.   ")]
    [InlineData("Hotovo.\n")]
    [InlineData("Hotovo.\r\n\t")]
    public void StripTrailingCivility_NoTrailingCivilityWithTrailingWhitespace_ReturnsUnchanged(string input)
    {
        // No civility to strip → the trimmer must not silently normalize
        // trailing whitespace the user (or Whisper) produced. The only time
        // we discard trailing whitespace is when it precedes a civility we
        // actually removed — in the no-op case the input is returned verbatim.
        Assert.Equal(input, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_CivilityMidSentence_DoesNotStrip()
    {
        // "Díky" here is inside the sentence, not a standalone trailing phrase.
        // Substring matching would incorrectly strip this — we require the
        // civility to BE the final sentence.
        var input = "Díky tomu bychom to měli hotovo rychleji.";

        Assert.Equal(input, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_CivilityEndingLongerSentence_DoesNotStrip()
    {
        // Real case: "V pohodě, děkuji za informaci a teď pokračujeme."
        // The final sentence is more than civility, so we keep it.
        var input = "V pohodě, děkuji za informaci a teď pokračujeme.";

        Assert.Equal(input, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_RozumimDekuji_DoesNotStripBecauseCommaJoined()
    {
        // "Rozumím, děkuji" is one sentence (comma-joined). Even though it
        // ends in "děkuji.", the trimmer should see the full content and bail.
        var input = "Rozumím, děkuji.";

        Assert.Equal(input, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_RealTranscription_Filmy_StripsTrailing()
    {
        // Verbatim from voice_transcriptions: a multi-sentence request ending
        // in a standalone "Děkuji." that adds nothing for the LLM agent.
        var input = "Chtěl bych vytvořit seznam filmů. Budu je stahovat a nahrávat. Děkuji.";
        var expected = "Chtěl bych vytvořit seznam filmů. Budu je stahovat a nahrávat.";

        Assert.Equal(expected, CivilityTrimmer.StripTrailingCivility(input));
    }

    [Fact]
    public void StripTrailingCivility_TrailingWhitespaceAfterCivility_HandledCleanly()
    {
        Assert.Equal("Hotovo.", CivilityTrimmer.StripTrailingCivility("Hotovo. Děkuji.   "));
    }

    [Fact]
    public void StripTrailingCivility_UnknownPolitePhrase_DoesNotStrip()
    {
        // "Zdravím." is polite but not in our list — we keep the conservative
        // set to avoid stripping things the user might want preserved.
        var input = "Hotovo. Zdravím.";

        Assert.Equal(input, CivilityTrimmer.StripTrailingCivility(input));
    }
}
