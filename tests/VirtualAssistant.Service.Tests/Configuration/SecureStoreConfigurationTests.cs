using Olbrasoft.VirtualAssistant.Service.Configuration;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Configuration;

/// <summary>
/// Unit tests for SecureStoreConfigurationProvider and related classes.
/// </summary>
public class SecureStoreConfigurationTests
{
    private readonly string _homeDirectory;

    public SecureStoreConfigurationTests()
    {
        _homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    #region ExpandPath Tests

    [Fact]
    public void ExpandPath_NullPath_ReturnsNull()
    {
        // Arrange & Act
        var result = SecureStoreConfigurationExtensions.ExpandPath(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExpandPath_EmptyPath_ReturnsEmpty()
    {
        // Arrange & Act
        var result = SecureStoreConfigurationExtensions.ExpandPath(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExpandPath_TildeOnly_ReturnsHomeDirectory()
    {
        // Arrange & Act
        var result = SecureStoreConfigurationExtensions.ExpandPath("~");

        // Assert
        Assert.Equal(_homeDirectory, result);
    }

    [Fact]
    public void ExpandPath_TildeSlashPath_ExpandsToHomePath()
    {
        // Arrange
        var relativePath = "~/some/path/file.txt";

        // Act
        var result = SecureStoreConfigurationExtensions.ExpandPath(relativePath);

        // Assert
        var expected = Path.Combine(_homeDirectory, "some/path/file.txt");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandPath_TildeWithoutSlash_ReturnsAsIs()
    {
        // Arrange - Path like ~username (Unix-style, not supported)
        var path = "~username/path";

        // Act
        var result = SecureStoreConfigurationExtensions.ExpandPath(path);

        // Assert - Should return as-is since ~username is not supported
        Assert.Equal(path, result);
    }

    [Fact]
    public void ExpandPath_AbsolutePath_ReturnsAsIs()
    {
        // Arrange
        var path = "/var/lib/secrets/secrets.json";

        // Act
        var result = SecureStoreConfigurationExtensions.ExpandPath(path);

        // Assert
        Assert.Equal(path, result);
    }

    [Fact]
    public void ExpandPath_RelativePath_ReturnsAsIs()
    {
        // Arrange
        var path = "config/secrets.json";

        // Act
        var result = SecureStoreConfigurationExtensions.ExpandPath(path);

        // Assert
        Assert.Equal(path, result);
    }

    #endregion

    #region SecureStoreConfigurationProvider Tests

    [Fact]
    public void Load_MissingSecretsFile_DoesNotThrow()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "secrets.json");
        var keyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "secrets.key");
        var provider = new SecureStoreConfigurationProvider(nonExistentPath, keyPath);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => provider.Load());
        Assert.Null(exception);
    }

    [Fact]
    public void Load_MissingKeyFile_DoesNotThrow()
    {
        // Arrange - Create a temp secrets file but no key file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var secretsPath = Path.Combine(tempDir, "secrets.json");
        var keyPath = Path.Combine(tempDir, "nonexistent.key");

        try
        {
            // Create an empty secrets file (won't be valid SecureStore format, but we're testing file existence check)
            File.WriteAllText(secretsPath, "{}");

            var provider = new SecureStoreConfigurationProvider(secretsPath, keyPath);

            // Act & Assert - Should not throw (key file doesn't exist)
            var exception = Record.Exception(() => provider.Load());
            Assert.Null(exception);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_InvalidVaultFormat_DoesNotThrow()
    {
        // Arrange - Create temp files with invalid content
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var secretsPath = Path.Combine(tempDir, "secrets.json");
        var keyPath = Path.Combine(tempDir, "secrets.key");

        try
        {
            // Create invalid files
            File.WriteAllText(secretsPath, "not a valid securestore vault");
            File.WriteAllText(keyPath, "not a valid key");

            var provider = new SecureStoreConfigurationProvider(secretsPath, keyPath);

            // Act & Assert - Should not throw (graceful degradation)
            var exception = Record.Exception(() => provider.Load());
            Assert.Null(exception);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    #endregion

    #region SecureStoreConfigurationSource Tests

    [Fact]
    public void Build_ReturnsSecureStoreConfigurationProvider()
    {
        // Arrange
        var source = new SecureStoreConfigurationSource
        {
            SecretsPath = "/path/to/secrets.json",
            KeyPath = "/path/to/secrets.key"
        };

        // Act
        var provider = source.Build(null!);

        // Assert
        Assert.IsType<SecureStoreConfigurationProvider>(provider);
    }

    #endregion
}
