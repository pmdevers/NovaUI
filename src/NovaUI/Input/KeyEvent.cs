namespace NovaUI.Input;

public enum Key { None, Backspace, Delete, Left, Right, Up, Down, Home, End, Enter, Tab, Escape }

[Flags]
public enum KeyModifiers { None = 0, Shift = 1, Control = 2, Alt = 4, Meta = 8 }

/// <summary>A key press. <see cref="Text"/> holds typed characters (null for pure navigation/command keys).</summary>
public readonly record struct KeyEvent(Key Key, string? Text = null, KeyModifiers Modifiers = KeyModifiers.None);

/// <summary>Receives keyboard input while focused.</summary>
public interface IKeyHandler
{
    /// <summary>Returns true if the key was consumed.</summary>
    bool HandleKey(KeyEvent e);
}

/// <summary>Tracks which handler currently receives keyboard input.</summary>
public sealed class FocusManager
{
    public IKeyHandler? Current { get; private set; }

    public void Request(IKeyHandler handler) => Current = handler;

    /// <summary>Drops focus; returns true if something had it.</summary>
    public bool Clear()
    {
        var had = Current is not null;
        Current = null;
        return had;
    }
}
