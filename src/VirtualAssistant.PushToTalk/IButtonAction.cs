namespace Olbrasoft.VirtualAssistant.PushToTalk;

/// <summary>
/// Interface for button action handlers (Strategy pattern).
/// Allows different actions to be executed based on click patterns.
/// </summary>
public interface IButtonAction
{
    /// <summary>
    /// Gets the name of this action for logging purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the action asynchronously.
    /// </summary>
    /// <returns>Task representing the async operation.</returns>
    Task ExecuteAsync();
}

/// <summary>
/// Action that simulates a single key press.
/// </summary>
public class KeyPressAction : IButtonAction
{
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly KeyCode _keyCode;
    private readonly bool _raiseReleaseEvent;

    public KeyPressAction(IKeyboardMonitor keyboardMonitor, KeyCode keyCode, string name, bool raiseReleaseEvent = true)
    {
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _keyCode = keyCode;
        Name = name;
        _raiseReleaseEvent = raiseReleaseEvent;
    }

    public string Name { get; }

    public async Task ExecuteAsync()
    {
        await _keyboardMonitor.SimulateKeyPressAsync(_keyCode);
        if (_raiseReleaseEvent)
        {
            await Task.Delay(MultiClickDetector.KeySimulationDelayMs);
            _keyboardMonitor.RaiseKeyReleasedEvent(_keyCode);
        }
    }
}

/// <summary>
/// Action that simulates a key combination (modifier + key).
/// </summary>
public class KeyComboAction : IButtonAction
{
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly KeyCode _modifier;
    private readonly KeyCode _key;

    public KeyComboAction(IKeyboardMonitor keyboardMonitor, KeyCode modifier, KeyCode key, string name)
    {
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _modifier = modifier;
        _key = key;
        Name = name;
    }

    public string Name { get; }

    public async Task ExecuteAsync()
    {
        await _keyboardMonitor.SimulateKeyComboAsync(_modifier, _key);
    }
}

/// <summary>
/// Action that simulates a key combination with two modifiers (modifier1 + modifier2 + key).
/// </summary>
public class KeyComboWithTwoModifiersAction : IButtonAction
{
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly KeyCode _modifier1;
    private readonly KeyCode _modifier2;
    private readonly KeyCode _key;

    public KeyComboWithTwoModifiersAction(
        IKeyboardMonitor keyboardMonitor,
        KeyCode modifier1,
        KeyCode modifier2,
        KeyCode key,
        string name)
    {
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _modifier1 = modifier1;
        _modifier2 = modifier2;
        _key = key;
        Name = name;
    }

    public string Name { get; }

    public async Task ExecuteAsync()
    {
        await _keyboardMonitor.SimulateKeyComboAsync(_modifier1, _modifier2, _key);
    }
}

/// <summary>
/// Action that executes a shell command.
/// </summary>
public class ShellCommandAction : IButtonAction
{
    private readonly string _command;

    public ShellCommandAction(string command, string name)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        Name = name;
    }

    public string Name { get; }

    public Task ExecuteAsync()
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{_command}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Null action that does nothing (for single-click when no action is needed).
/// </summary>
public class NoAction : IButtonAction
{
    public static readonly NoAction Instance = new();

    private NoAction() { }

    public string Name => "NoAction";

    public Task ExecuteAsync() => Task.CompletedTask;
}
