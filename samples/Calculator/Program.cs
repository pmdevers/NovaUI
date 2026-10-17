using NovaUI;
using NovaUI.Desktop;
using NovaUI.Widgets;

await App.RunAsync<CalculatorWindow>(new NovaUI.Skia.SkiaRasterizer());

sealed class CalculatorWindow : Window
{
    private readonly Widget _shell;

    public CalculatorWindow() =>
        _shell = new CalculatorShell(new CalculatorWidget());

    public override string Title => "Calculator";

    public override Widget Build() => _shell;
}
