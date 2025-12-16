using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

public class CommandDetectionServiceTests
{
    private readonly CommandDetectionService _service;

    public CommandDetectionServiceTests()
    {
        _service = new CommandDetectionService();
    }

    #region IsStopCommand Tests

    [Theory]
    [InlineData("stop")]
    [InlineData("STOP")]
    [InlineData("Stop")]
    [InlineData("please stop")]
    [InlineData("stop now")]
    [InlineData("can you stop?")]
    public void IsStopCommand_WithStopWord_ReturnsTrue(string text)
    {
        var result = _service.IsStopCommand(text);
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsStopCommand_WithEmptyOrNull_ReturnsFalse(string? text)
    {
        var result = _service.IsStopCommand(text!);
        Assert.False(result);
    }

    [Theory]
    [InlineData("continue")]
    [InlineData("hello")]
    [InlineData("nonstop")]
    [InlineData("stopped")]
    [InlineData("stopping")]
    public void IsStopCommand_WithoutStopWord_ReturnsFalse(string text)
    {
        var result = _service.IsStopCommand(text);
        Assert.False(result);
    }

    #endregion

    #region IsCancelCommand Tests

    [Theory]
    [InlineData("cancel")]
    [InlineData("CANCEL")]
    [InlineData("Cancel")]
    [InlineData("please cancel")]
    [InlineData("kencel")]  // Whisper transcription variant
    public void IsCancelCommand_WithCancelWord_ReturnsTrue(string text)
    {
        var result = _service.IsCancelCommand(text);
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsCancelCommand_WithEmptyOrNull_ReturnsFalse(string? text)
    {
        var result = _service.IsCancelCommand(text!);
        Assert.False(result);
    }

    [Theory]
    [InlineData("continue")]
    [InlineData("hello")]
    [InlineData("cancelled")]
    [InlineData("cancelling")]
    public void IsCancelCommand_WithoutCancelWord_ReturnsFalse(string text)
    {
        var result = _service.IsCancelCommand(text);
        Assert.False(result);
    }

    #endregion

    #region ShouldSkipLocally Tests

    [Theory]
    [InlineData("ano")]
    [InlineData("ne")]
    [InlineData("jo")]
    [InlineData("ok")]
    [InlineData("hmm")]
    [InlineData("dobre")]
    [InlineData("jasne")]
    [InlineData("diky")]
    [InlineData("prosim")]
    [InlineData("ANO")]
    [InlineData("Ne")]
    public void ShouldSkipLocally_WithNoisePattern_ReturnsTrue(string text)
    {
        var result = _service.ShouldSkipLocally(text);
        Assert.True(result);
    }

    [Theory]
    [InlineData("ano.")]
    [InlineData("ok!")]
    [InlineData("jasne?")]
    [InlineData("dobre,")]
    public void ShouldSkipLocally_WithNoisePatternAndPunctuation_ReturnsTrue(string text)
    {
        var result = _service.ShouldSkipLocally(text);
        Assert.True(result);
    }

    [Theory]
    [InlineData("otevri soubor")]
    [InlineData("spust build")]
    [InlineData("jak se mas")]
    [InlineData("hello world")]
    public void ShouldSkipLocally_WithMeaningfulText_ReturnsFalse(string text)
    {
        var result = _service.ShouldSkipLocally(text);
        Assert.False(result);
    }

    #endregion
}
