using NovaUI;
using NovaUI.Desktop;
using NovaUI.Rendering;
using NovaUI.Widgets;

await App.RunAsync<FormWindow>();

sealed class FormWindow : Window
{
    private readonly TextInputState _name = new();
    private readonly State<string> _greeting = new("Type your name");
    private readonly State<bool> _subscribe = new(false);
    private readonly State<string> _size = new("M");
    private readonly State<float> _volume = new(40);
    private readonly ComboBoxState _country = new(["Netherlands", "Germany", "Belgium"]);
    private readonly Image _icon = Image.Create(16, 16, (x, y) =>
        (x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f) < 49 ? new Color(76, 175, 80) : Color.Transparent);

    public FormWindow() => _name.Submitted += name => _greeting.Value = $"Hello, {name}!";

    public override string Title => "Form";

    public override Widget Build() =>
        new Padding(EdgeInsets.All(24),
            new Column(
                new Text(_greeting.Value, 20),
                new TextInput(_name, "Your name"),
                new Checkbox("Subscribe to newsletter", _subscribe.Value, v => _subscribe.Value = v),
                new RadioBox<string>("Small", "S", _size.Value, v => _size.Value = v),
                new RadioBox<string>("Medium", "M", _size.Value, v => _size.Value = v),
                new RadioBox<string>("Large", "L", _size.Value, v => _size.Value = v),
                new Text($"Volume: {_volume.Value:0}"),
                new Slider(_volume.Value, 0, 100, v => _volume.Value = v, step: 1),
                new ComboBox(_country, "Country"),
                new ImageButton(_icon, () => _greeting.Value = $"Hello, {_name.Text}!", "Greet")));
}
