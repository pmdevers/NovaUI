using NovaUI;
using NovaUI.Desktop;
using NovaUI.Widgets;

await App.RunAsync<CounterWindow>();

sealed class CounterWindow : Window
{
    private readonly State<int> _count = new(0);

    public override string Title => "Counter";

    public override Widget Build() =>
        new Padding(EdgeInsets.All(24),
            new Column(
                new Text($"Count: {_count.Value}", 24),
                new Button("Increment", () => _count.Value++)));
}
