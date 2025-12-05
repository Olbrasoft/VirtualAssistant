using Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

namespace VirtualAssistant.PushToTalk.Service.Tests;

public class TranscriptionHistoryTests
{
    private readonly TranscriptionHistory _history;

    public TranscriptionHistoryTests()
    {
        _history = new TranscriptionHistory();
    }

    [Fact]
    public void LastText_InitialState_ReturnsNull()
    {
        // Assert
        Assert.Null(_history.LastText);
    }

    [Fact]
    public void SaveText_ValidText_StoresText()
    {
        // Arrange
        var text = "Hello, world!";

        // Act
        _history.SaveText(text);

        // Assert
        Assert.Equal(text, _history.LastText);
    }

    [Fact]
    public void SaveText_MultipleTimes_OverwritesPrevious()
    {
        // Arrange
        var firstText = "First text";
        var secondText = "Second text";

        // Act
        _history.SaveText(firstText);
        _history.SaveText(secondText);

        // Assert
        Assert.Equal(secondText, _history.LastText);
    }

    [Fact]
    public void SaveText_NullText_DoesNotOverwrite()
    {
        // Arrange
        var text = "Original text";
        _history.SaveText(text);

        // Act
        _history.SaveText(null!);

        // Assert
        Assert.Equal(text, _history.LastText);
    }

    [Fact]
    public void SaveText_EmptyString_DoesNotOverwrite()
    {
        // Arrange
        var text = "Original text";
        _history.SaveText(text);

        // Act
        _history.SaveText("");

        // Assert
        Assert.Equal(text, _history.LastText);
    }

    [Fact]
    public void SaveText_WhitespaceOnly_DoesNotOverwrite()
    {
        // Arrange
        var text = "Original text";
        _history.SaveText(text);

        // Act
        _history.SaveText("   ");

        // Assert
        Assert.Equal(text, _history.LastText);
    }

    [Fact]
    public void Clear_AfterSave_ReturnsNull()
    {
        // Arrange
        _history.SaveText("Some text");

        // Act
        _history.Clear();

        // Assert
        Assert.Null(_history.LastText);
    }

    [Fact]
    public void Clear_WhenEmpty_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var exception = Record.Exception(() => _history.Clear());
        Assert.Null(exception);
    }

    [Fact]
    public async Task SaveText_IsThreadSafe_MultipleCalls()
    {
        // Arrange
        var tasks = new List<Task>();
        var textValues = Enumerable.Range(1, 100).Select(i => $"Text {i}").ToList();

        // Act
        foreach (var text in textValues)
        {
            tasks.Add(Task.Run(() => _history.SaveText(text)));
        }

        await Task.WhenAll(tasks);

        // Assert - LastText should be one of the saved values (not null or corrupted)
        Assert.NotNull(_history.LastText);
        Assert.StartsWith("Text ", _history.LastText);
    }

    [Fact]
    public async Task LastText_IsThreadSafe_ConcurrentReadWrite()
    {
        // Arrange
        var writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                _history.SaveText($"Text {i}");
            }
        });

        var readTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                _ = _history.LastText; // Should not throw or return corrupted data
            }
        });

        // Act & Assert - should not throw
        await Task.WhenAll(writeTask, readTask);
    }

    [Fact]
    public void SaveText_CzechCharacters_PreservesText()
    {
        // Arrange
        var czechText = "Příliš žluťoučký kůň úpěl ďábelské ódy";

        // Act
        _history.SaveText(czechText);

        // Assert
        Assert.Equal(czechText, _history.LastText);
    }

    [Fact]
    public void SaveText_LongText_PreservesText()
    {
        // Arrange
        var longText = new string('a', 10000);

        // Act
        _history.SaveText(longText);

        // Assert
        Assert.Equal(longText, _history.LastText);
    }
}
