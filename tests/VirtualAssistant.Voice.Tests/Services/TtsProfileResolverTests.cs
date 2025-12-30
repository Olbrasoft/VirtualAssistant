using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for TtsProfileResolver service.
/// Tests application-specific TTS profile resolution.
/// </summary>
public class TtsProfileResolverTests
{
    [Fact]
    public void GetProfileForApplication_WithKnownApp_ReturnsCorrectProfile()
    {
        // Arrange
        var options = Options.Create(new TtsProfilesOptions
        {
            Profiles = new Dictionary<string, TtsProfile>
            {
                ["claude-code"] = new()
                {
                    Voice = "cs-CZ-AntoninNeural",
                    Rate = 10,
                    Pitch = 0,
                    Priority = 10
                }
            }
        });
        var resolver = new TtsProfileResolver(options, NullLogger<TtsProfileResolver>.Instance);

        // Act
        var config = resolver.GetProfileForApplication("claude-code");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("cs-CZ-AntoninNeural", config.Voice);
        Assert.Equal("10", config.Rate);
        Assert.Equal("0", config.Pitch);
        Assert.Equal("100", config.Volume); // Default volume
    }

    [Fact]
    public void GetProfileForApplication_WithUnknownApp_ReturnsDefaultProfile()
    {
        // Arrange
        var options = Options.Create(new TtsProfilesOptions
        {
            Profiles = new Dictionary<string, TtsProfile>
            {
                ["claude-code"] = new()
                {
                    Voice = "cs-CZ-AntoninNeural",
                    Rate = 10
                }
            },
            DefaultProfile = new TtsProfile
            {
                Voice = "cs-CZ-VlastaNeural",
                Rate = 0,
                Pitch = 0
            }
        });
        var resolver = new TtsProfileResolver(options, NullLogger<TtsProfileResolver>.Instance);

        // Act
        var config = resolver.GetProfileForApplication("unknown-app");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("cs-CZ-VlastaNeural", config.Voice);
        Assert.Equal("0", config.Rate);
        Assert.Equal("0", config.Pitch);
    }

    [Fact]
    public void GetProfileForApplication_WithNullAppName_ReturnsDefaultProfile()
    {
        // Arrange
        var options = Options.Create(new TtsProfilesOptions
        {
            Profiles = new Dictionary<string, TtsProfile>
            {
                ["claude-code"] = new()
                {
                    Voice = "cs-CZ-AntoninNeural",
                    Rate = 10
                }
            },
            DefaultProfile = new TtsProfile
            {
                Voice = "cs-CZ-Default",
                Rate = 5,
                Pitch = 0,
                Priority = 1
            }
        });
        var resolver = new TtsProfileResolver(options, NullLogger<TtsProfileResolver>.Instance);

        // Act
        var config = resolver.GetProfileForApplication(null);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("cs-CZ-Default", config.Voice);
        Assert.Equal("5", config.Rate);
    }

    [Fact]
    public void GetProfileForApplication_WithEmptyAppName_ReturnsDefaultProfile()
    {
        // Arrange
        var options = Options.Create(new TtsProfilesOptions
        {
            DefaultProfile = new TtsProfile
            {
                Voice = "cs-CZ-SystemVoice",
                Rate = 0
            }
        });
        var resolver = new TtsProfileResolver(options, NullLogger<TtsProfileResolver>.Instance);

        // Act
        var config = resolver.GetProfileForApplication("");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("cs-CZ-SystemVoice", config.Voice);
    }

    [Fact]
    public void GetProfileForApplication_MultipleCalls_ReturnsSameVoiceConfig()
    {
        // Arrange
        var options = Options.Create(new TtsProfilesOptions
        {
            Profiles = new Dictionary<string, TtsProfile>
            {
                ["test-app"] = new()
                {
                    Voice = "test-voice",
                    Rate = 20,
                    Pitch = 10
                }
            }
        });
        var resolver = new TtsProfileResolver(options, NullLogger<TtsProfileResolver>.Instance);

        // Act
        var config1 = resolver.GetProfileForApplication("test-app");
        var config2 = resolver.GetProfileForApplication("test-app");

        // Assert - both calls return same configuration
        Assert.Equal(config1.Voice, config2.Voice);
        Assert.Equal(config1.Rate, config2.Rate);
        Assert.Equal(config1.Pitch, config2.Pitch);
    }
}
