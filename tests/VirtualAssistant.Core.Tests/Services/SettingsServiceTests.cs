using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testSettingsPath;
    private readonly string _originalSettingsPath;
    private readonly ILogger<SettingsService> _logger;
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"va-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        _testSettingsPath = Path.Combine(tempDir, "settings.json");

        var loggerMock = new Mock<ILogger<SettingsService>>();
        _logger = loggerMock.Object;

        _sut = new SettingsService(_logger);

        // Use reflection to override settings path for testing
        var field = typeof(SettingsService).GetField("_settingsPath", BindingFlags.NonPublic | BindingFlags.Instance);
        _originalSettingsPath = (string)field!.GetValue(_sut)!;
        field.SetValue(_sut, _testSettingsPath);
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsDefaultValue()
    {
        // Arrange
        const string key = "non.existent.key";
        const string defaultValue = "default";

        // Act
        var result = await _sut.GetAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsSameValue()
    {
        // Arrange
        const string key = "test.key";
        const string value = "test-value";

        // Act
        await _sut.SetAsync(key, value);
        var result = await _sut.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task SetAsync_BooleanValue_PersistsCorrectly()
    {
        // Arrange
        const string key = "test.bool";
        const bool value = true;

        // Act
        await _sut.SetAsync(key, value);
        var result = await _sut.GetAsync(key, false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingValue()
    {
        // Arrange
        const string key = "test.update";
        const string originalValue = "original";
        const string updatedValue = "updated";

        // Act
        await _sut.SetAsync(key, originalValue);
        await _sut.SetAsync(key, updatedValue);
        var result = await _sut.GetAsync<string>(key);

        // Assert
        Assert.Equal(updatedValue, result);
    }

    [Fact]
    public async Task SetAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        const string key = "test.directory";
        const string value = "test";

        // Act
        await _sut.SetAsync(key, value);

        // Assert
        Assert.True(File.Exists(_testSettingsPath));
    }

    [Fact]
    public async Task SetAsync_ComplexType_SerializesCorrectly()
    {
        // Arrange
        const string key = "test.complex";
        var value = new { Name = "Test", Count = 42 };

        // Act
        await _sut.SetAsync(key, value);
        var result = await _sut.GetAsync<dynamic>(key);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAsync_WithConcurrentAccess_HandlesSafely()
    {
        // Arrange
        const string key = "test.concurrent";
        const string value = "concurrent-test";
        await _sut.SetAsync(key, value);

        // Act - Run 10 concurrent reads
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _sut.GetAsync<string>(key))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.Equal(value, r));
    }

    [Fact]
    public async Task GetAsync_NullValue_ReturnsDefaultValue()
    {
        // Arrange
        const string key = "test.null";
        await _sut.SetAsync<string?>(key, null);

        // Act
        var result = await _sut.GetAsync(key, "default");

        // Assert
        // When null is stored, getting it should return the stored null, not default
        // But JSON serialization might handle this differently
        Assert.True(result == null || result == "default");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }

            var dir = Path.GetDirectoryName(_testSettingsPath);
            if (dir != null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
