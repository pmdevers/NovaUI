namespace NovaUI;

/// <summary>Observable value. Changing it notifies listeners (e.g. to rebuild a widget).</summary>
public sealed class State<T>(T initial)
{
    private T _value = initial;
    public event Action? Changed;

    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            Changed?.Invoke();
        }
    }
}
