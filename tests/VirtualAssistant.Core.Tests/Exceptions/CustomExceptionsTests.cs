using Olbrasoft.VirtualAssistant.Core.Exceptions;
using Xunit;

namespace VirtualAssistant.Core.Tests.Exceptions;

public class CustomExceptionsTests
{
    [Fact]
    public void VirtualAssistantException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new VirtualAssistantException();

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<VirtualAssistantException>(exception);
    }

    [Fact]
    public void VirtualAssistantException_WithMessage_StoresMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var exception = new VirtualAssistantException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void VirtualAssistantException_WithInnerException_StoresInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var message = "Outer error";

        // Act
        var exception = new VirtualAssistantException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void AudioCaptureException_InheritsFromVirtualAssistantException()
    {
        // Act
        var exception = new AudioCaptureException("Audio capture failed");

        // Assert
        Assert.IsAssignableFrom<VirtualAssistantException>(exception);
    }

    [Fact]
    public void TranscriptionException_InheritsFromVirtualAssistantException()
    {
        // Act
        var exception = new TranscriptionException("Transcription failed");

        // Assert
        Assert.IsAssignableFrom<VirtualAssistantException>(exception);
    }

    [Fact]
    public void ConfigurationException_WithConfigurationKey_StoresKey()
    {
        // Arrange
        var message = "Configuration invalid";
        var configKey = "ConnectionStrings:DefaultConnection";

        // Act
        var exception = new ConfigurationException(message, configKey);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(configKey, exception.ConfigurationKey);
    }

    [Fact]
    public void ConfigurationException_WithInnerException_PreservesConfigurationKey()
    {
        // Arrange
        var message = "Configuration invalid";
        var configKey = "SomeKey";
        var innerException = new FileNotFoundException("File not found");

        // Act
        var exception = new ConfigurationException(message, configKey, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(configKey, exception.ConfigurationKey);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TrayServiceException_InheritsFromVirtualAssistantException()
    {
        // Act
        var exception = new TrayServiceException("Tray service failed");

        // Assert
        Assert.IsAssignableFrom<VirtualAssistantException>(exception);
    }

    [Fact]
    public void VadException_InheritsFromVirtualAssistantException()
    {
        // Act
        var exception = new VadException("VAD model failed");

        // Assert
        Assert.IsAssignableFrom<VirtualAssistantException>(exception);
    }

    [Fact]
    public void AllCustomExceptions_CanBeCaughtAsVirtualAssistantException()
    {
        // Arrange
        var exceptions = new Exception[]
        {
            new AudioCaptureException("Audio error"),
            new TranscriptionException("Transcription error"),
            new ConfigurationException("Config error"),
            new TrayServiceException("Tray error"),
            new VadException("VAD error")
        };

        // Act & Assert
        foreach (var exception in exceptions)
        {
            Assert.IsAssignableFrom<VirtualAssistantException>(exception);
        }
    }
}
