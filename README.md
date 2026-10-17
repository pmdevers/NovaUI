# NovaUI

Code-first, cross-platform desktop UI for C# (in the spirit of Flutter / egui). No XAML or markup: UIs are plain C#.

```csharp
await App.RunAsync<MainWindow>();

class MainWindow : Window
{
    private readonly State<int> _count = new(0);

    public override Widget Build() => new Column(
        new Text($"Count: {_count.Value}", 24),
        new Button("Increment", () => _count.Value++));
}
```

## Layout

| Project | Purpose |
|---|---|
| `src/NovaUI` | Core: geometry, widgets, layout, state, `ICanvas` abstraction. No platform deps. |
| `src/NovaUI.Skia` | Optional SkiaSharp `IRasterizer` (full-quality text). Pass `new SkiaRasterizer()` to `App.RunAsync`. |
| `src/NovaUI.Desktop` | `App`, `Window`, `AppContext` / `WindowManager`; native windows with a built-in dependency-free software rasterizer by default (text via FreeType on Linux; Windows/macOS currently draw placeholder text blocks), presented via Vulkan (Linux/X11), Direct3D 11 (Windows) or Metal (macOS). No Silk.NET. |
| `samples/Counter`, `samples/Calculator`, `samples/Form` | Sample apps (`Form` shows `TextInput`). |
| `tests/NovaUI.Tests` | xUnit tests for core. |

## Build

Requires .NET 10 SDK: `dotnet build NovaUI.slnx && dotnet test NovaUI.slnx`
