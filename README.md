# NovaUI

Code-first, cross-platform desktop UI for C# (in the spirit of Flutter / egui). No XAML or markup: UIs are plain C#.

```csharp
var count = new State<int>(0);
Desktop.Run(() => new Column(
    new Text($"Count: {count.Value}", 24),
    new Button("Increment", () => count.Value++)));
```

## Layout

| Project | Purpose |
|---|---|
| `src/NovaUI` | Core: geometry, widgets, layout, state, `ICanvas` abstraction. No platform deps. |
| `src/NovaUI.Skia` | SkiaSharp `ICanvas` / text measurer. |
| `src/NovaUI.Desktop` | Window, mouse input and render loop (Silk.NET + Skia). |
| `samples/Counter` | Sample app. |
| `tests/NovaUI.Tests` | xUnit tests for core. |

## Build

Requires .NET 10 SDK: `dotnet build NovaUI.slnx && dotnet test NovaUI.slnx`
