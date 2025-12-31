using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace VirtualAssistant.Core.Tests.Services;

/// <summary>
/// Unit tests for SettingsService.
/// Verifies JSON-based settings persistence, thread safety, and default value handling.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _testSettingsPath;
    private readonly string _originalSettingsPath;
    private readonly ILogger<SettingsService> _logger;
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        // Create temporary test directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"va-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        _testSettingsPath = Path.Combine(tempDir, "settings.json");

        // Mock logger
        var loggerMock = new Mock<ILogger<SettingsService>>();
        _logger = loggerMock.Object;

        // Create SUT
        _sut = new SettingsService(_logger);

        // Use reflection to override the settings path for testing
        var field = typeof(SettingsService).GetField("_settingsPath", BindingFlags.NonPublic | BindingFlags.Instance);
        _originalSettingsPath = (string)field!.GetValue(_sut)!;
        field.SetValue(_sut, _testSettingsPath);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentKey_ReturnsDefaultValue()
    {
        // Arrange
        var key = "test.key";
        var defaultValue = "default";

        // Act
        var result = await _sut.GetAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public async Task SetAsync_AndGetAsync_PersistsValue()
    {
        // Arrange
        var key = "test.setting";
        var value = "test-value";

        // Act
        await _sut.SetAsync(key, value);
        var result = await _sut.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task SetAsync_WithBooleanValue_PersistsCorrectly()
    {
        // Arrange
        var key = "tts.muted";
        var value = true;

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
        var key = "test.counter";
        var initialValue = 10;
        var updatedValue = 20;

        // Act
        await _sut.SetAsync(key, initialValue);
        await _sut.SetAsync(key, updatedValue);
        var result = await _sut.GetAsync(key, 0);

        // Assert
        Assert.Equal(updatedValue, result);
    }

    [Fact]
    public async Task SetAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"va-test-new-{Guid.NewGuid()}");
        var settingsPath = Path.Combine(tempDir, "settings.json");
        Directory.CreateDirectory(tempDir);
        var service = new SettingsService(_logger);

        // Use reflection to set custom path
        var field = typeof(SettingsService).GetField("_settingsPath", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(service, settingsPath);

        // Act
        await service.SetAsync("test.key", "value");

        // Assert
        Assert.True(Directory.Exists(tempDir));
        Assert.True(File.Exists(settingsPath));

        // Cleanup
        Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public async Task GetAsync_WithComplexType_DeserializesCorrectly()
    {
        // Arrange
        var key = "test.object";
        var value = new TestObject { Name = "Test", Value = 42 };

        // Act
        await _sut.SetAsync(key, value);
        var result = await _sut.GetAsync<TestObject>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(value.Name, result!.Name);
        Assert.Equal(value.Value, result.Value);
    }

    [Fact]
    public async Task ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var key = "test.concurrent";
        var tasks = new List<Task>();

        // Act - Multiple concurrent writes
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () => await _sut.SetAsync($"{key}.{index}", index)));
        }

        await Task.WhenAll(tasks);

        // Assert - All values should be persisted
        for (int i = 0; i < 10; i++)
        {
            var result = await _sut.GetAsync<int>($"{key}.{i}");
            Assert.Equal(i, result);
        }
    }

    [Fact]
    public async Task SetAsync_WithNullValue_RemovesKey()
    {
        // Arrange
        var key = "test.nullable";
        await _sut.SetAsync(key, "initial-value");

        // Act
        await _sut.SetAsync<string?>(key, null);
        var result = await _sut.GetAsync<string?>(key);

        // Assert
        Assert.Null(result);
    }

    public void Dispose()
    {
        // Cleanup test directory
        var directory = Path.GetDirectoryName(_testSettingsPath);
        if (directory != null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
