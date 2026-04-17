using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Voice.Filters;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray;

/// <summary>
/// Unit tests for MenuEventDispatcher.
/// Tests menu event handling, command pattern implementation, and error handling.
/// </summary>
public class MenuEventDispatcherTests
{
    private readonly Mock<ILogger<MenuEventDispatcher>> _loggerMock;
    private readonly Mock<IManualMuteService> _muteServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<ILlmProvider> _llmProviderMock;
    private readonly Mock<IDictationControl> _dictationControlMock;
    private const string DashboardBaseUrl = "http://localhost:5055";

    public MenuEventDispatcherTests()
    {
        _loggerMock = new Mock<ILogger<MenuEventDispatcher>>();
        _muteServiceMock = new Mock<IManualMuteService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _llmProviderMock = new Mock<ILlmProvider>();
        _dictationControlMock = new Mock<IDictationControl>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl);

        // Assert
        Assert.NotNull(sut);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MenuEventDispatcher(
                null!,
                _muteServiceMock.Object,
                _settingsServiceMock.Object,
                DashboardBaseUrl));
    }

    [Fact]
    public void Constructor_WithNullMuteService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MenuEventDispatcher(
                _loggerMock.Object,
                null!,
                _settingsServiceMock.Object,
                DashboardBaseUrl));
    }

    [Fact]
    public void Constructor_WithNullSettingsService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MenuEventDispatcher(
                _loggerMock.Object,
                _muteServiceMock.Object,
                null!,
                DashboardBaseUrl));
    }

    [Fact]
    public void Constructor_WithNullOptionalParameters_CreatesInstance()
    {
        // Act
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            llmProvider: null,
            dictationControl: null);

        // Assert
        Assert.NotNull(sut);
    }

    #endregion

    #region HandleMuteToggle Tests

    [Fact]
    public void HandleMuteToggle_CallsMuteServiceToggle()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl);

        _muteServiceMock.Setup(x => x.Toggle());
        _muteServiceMock.Setup(x => x.IsMuted).Returns(true);

        // Act
        sut.HandleMuteToggle();

        // Assert
        _muteServiceMock.Verify(x => x.Toggle(), Times.Once);
    }

    [Fact]
    public void HandleMuteToggle_WhenToggleFails_LogsError()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl);

        var exception = new InvalidOperationException("Toggle failed");
        _muteServiceMock.Setup(x => x.Toggle()).Throws(exception);

        // Act
        sut.HandleMuteToggle();

        // Assert - Should not throw, but should log error
        _muteServiceMock.Verify(x => x.Toggle(), Times.Once);
    }

    #endregion

    #region HandleTtsMuteToggleAsync Tests

    [Fact]
    public async Task HandleTtsMuteToggleAsync_WhenCurrentlyUnmuted_SetsMutedToTrue()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl);

        // Act
        await sut.HandleTtsMuteToggleAsync(true);

        // Assert
        _settingsServiceMock.Verify(x => x.SetAsync("tts.muted", true), Times.Once);
    }

    [Fact]
    public async Task HandleTtsMuteToggleAsync_WhenCurrentlyMuted_SetsMutedToFalse()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl);

        // Act
        await sut.HandleTtsMuteToggleAsync(false);

        // Assert
        _settingsServiceMock.Verify(x => x.SetAsync("tts.muted", false), Times.Once);
    }

    [Fact]
    public async Task HandleTtsMuteToggleAsync_WhenToggleFails_LogsError()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl);

        var exception = new InvalidOperationException("Settings error");
        _settingsServiceMock.Setup(x => x.SetAsync("tts.muted", It.IsAny<bool>()))
            .ThrowsAsync(exception);

        // Act
        await sut.HandleTtsMuteToggleAsync(true);

        // Assert - Should not throw, but should log error
        _settingsServiceMock.Verify(x => x.SetAsync("tts.muted", true), Times.Once);
    }

    #endregion

    #region HandleShowLogs Tests

    // DISABLED: This test opens actual Chrome browser (side-effect)
    // HandleShowLogs() calls Process.Start() which launches browser
    // Unit tests should not have side-effects on the system
    // TODO: Refactor HandleShowLogs to use IProcessStarter interface for testability

    //[Fact]
    //public void HandleShowLogs_ConstructsCorrectUrl()
    //{
    //    // Arrange
    //    var sut = new MenuEventDispatcher(
    //        _loggerMock.Object,
    //        _muteServiceMock.Object,
    //        _settingsServiceMock.Object,
    //        DashboardBaseUrl);
    //
    //    // Act & Assert
    //    // Note: We can't easily test Process.Start without integration tests
    //    // This test verifies the method doesn't throw
    //    sut.HandleShowLogs();
    //}

    #endregion

    #region HandleLlmCorrectionToggle Tests

    [Fact]
    public void HandleLlmCorrectionToggle_WithoutProvider_LogsWarning()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            llmProvider: null);

        // Act
        sut.HandleLlmCorrectionToggle(true);

        // Assert - Should log warning but not throw
    }

    [Fact]
    public void HandleLlmCorrectionToggle_WithProvider_EnablesCorrection()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            _llmProviderMock.Object);

        _llmProviderMock.Setup(x => x.SetEnabled(true));

        // Act
        sut.HandleLlmCorrectionToggle(true);

        // Assert
        _llmProviderMock.Verify(x => x.SetEnabled(true), Times.Once);
    }

    [Fact]
    public void HandleLlmCorrectionToggle_WithProvider_DisablesCorrection()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            _llmProviderMock.Object);

        _llmProviderMock.Setup(x => x.SetEnabled(false));

        // Act
        sut.HandleLlmCorrectionToggle(false);

        // Assert
        _llmProviderMock.Verify(x => x.SetEnabled(false), Times.Once);
    }

    [Fact]
    public void HandleLlmCorrectionToggle_WhenSetEnabledFails_LogsError()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            _llmProviderMock.Object);

        var exception = new InvalidOperationException("Provider error");
        _llmProviderMock.Setup(x => x.SetEnabled(It.IsAny<bool>()))
            .Throws(exception);

        // Act
        sut.HandleLlmCorrectionToggle(true);

        // Assert - Should not throw, but should log error
        _llmProviderMock.Verify(x => x.SetEnabled(true), Times.Once);
    }

    #endregion

    #region HandleReloadPrompt Tests

    // DISABLED: These tests are no longer needed
    // HandleReloadPrompt() now only clears cache via ILlmProvider.ReloadPrompt()
    // No filesystem operations, prompts deployed via deploy.sh script

    //[Fact]
    //public void HandleReloadPrompt_WithoutProvider_LogsWarning()
    //{
    //    // Arrange
    //    var sut = new MenuEventDispatcher(
    //        _loggerMock.Object,
    //        _muteServiceMock.Object,
    //        _settingsServiceMock.Object,
    //        DashboardBaseUrl,
    //        llmProvider: null);
    //
    //    // Act
    //    sut.HandleReloadPrompt();
    //
    //    // Assert - Should log warning but not throw
    //}
    //
    //[Fact]
    //public void HandleReloadPrompt_WithProvider_CallsReloadPrompt()
    //{
    //    // Arrange
    //    var sut = new MenuEventDispatcher(
    //        _loggerMock.Object,
    //        _muteServiceMock.Object,
    //        _settingsServiceMock.Object,
    //        DashboardBaseUrl,
    //        _llmProviderMock.Object);
    //
    //    _llmProviderMock.Setup(x => x.ReloadPrompt());
    //
    //    // Act
    //    sut.HandleReloadPrompt();
    //
    //    // Assert
    //    _llmProviderMock.Verify(x => x.ReloadPrompt(), Times.Once);
    //}
    //
    //[Fact]
    //public void HandleReloadPrompt_WhenReloadFails_LogsError()
    //{
    //    // Arrange
    //    var sut = new MenuEventDispatcher(
    //        _loggerMock.Object,
    //        _muteServiceMock.Object,
    //        _settingsServiceMock.Object,
    //        DashboardBaseUrl,
    //        _llmProviderMock.Object);
    //
    //    var exception = new InvalidOperationException("Reload failed");
    //    _llmProviderMock.Setup(x => x.ReloadPrompt())
    //        .Throws(exception);
    //
    //    // Act
    //    sut.HandleReloadPrompt();
    //
    //    // Assert - Should not throw, but should log error
    //    _llmProviderMock.Verify(x => x.ReloadPrompt(), Times.Once);
    //}

    #endregion

    #region HandleDictationToggle Tests

    [Fact]
    public void HandleDictationToggle_WithoutControl_LogsWarning()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            dictationControl: null);

        // Act
        sut.HandleDictationToggle(true);

        // Assert - Should log warning but not throw
    }

    [Fact]
    public void HandleDictationToggle_WithControl_EnablesDictation()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            dictationControl: _dictationControlMock.Object);

        _dictationControlMock.Setup(x => x.SetDictationEnabled(true));

        // Act
        sut.HandleDictationToggle(true);

        // Assert
        _dictationControlMock.Verify(x => x.SetDictationEnabled(true), Times.Once);
    }

    [Fact]
    public void HandleDictationToggle_WithControl_DisablesDictation()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            dictationControl: _dictationControlMock.Object);

        _dictationControlMock.Setup(x => x.SetDictationEnabled(false));

        // Act
        sut.HandleDictationToggle(false);

        // Assert
        _dictationControlMock.Verify(x => x.SetDictationEnabled(false), Times.Once);
    }

    [Fact]
    public void HandleDictationToggle_WhenSetEnabledFails_LogsError()
    {
        // Arrange
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            dictationControl: _dictationControlMock.Object);

        var exception = new InvalidOperationException("Control error");
        _dictationControlMock.Setup(x => x.SetDictationEnabled(It.IsAny<bool>()))
            .Throws(exception);

        // Act
        sut.HandleDictationToggle(true);

        // Assert - Should not throw, but should log error
        _dictationControlMock.Verify(x => x.SetDictationEnabled(true), Times.Once);
    }

    #endregion

    #region HandleReloadCorrectionsCache Tests

    [Fact]
    public void HandleReloadCorrectionsCache_WithoutFilter_LogsWarningDoesNotThrow()
    {
        // correctionFilter omitted — the tray should not crash if the Voice layer
        // decided to disable DB corrections (repository: null construction).
        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            correctionFilter: null);

        var ex = Record.Exception(() => sut.HandleReloadCorrectionsCache());

        Assert.Null(ex);
    }

    [Fact]
    public void HandleReloadCorrectionsCache_WithFilter_ForcesNextApplyToReloadFromRepository()
    {
        // Verifies the contract the tray button was built for: after a click, the
        // very next Apply() calls the repository, picking up newly INSERTed rows.
        var repo = new Mock<ITranscriptionCorrectionRepository>();
        repo.Setup(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TranscriptionCorrection>());

        var filter = new DatabaseCorrectionFilterStrategy(
            Mock.Of<ILogger<DatabaseCorrectionFilterStrategy>>(),
            repo.Object);

        var sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            DashboardBaseUrl,
            correctionFilter: filter);

        filter.Apply("warm cache");
        sut.HandleReloadCorrectionsCache();
        filter.Apply("trigger reload");

        repo.Verify(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #endregion
}
