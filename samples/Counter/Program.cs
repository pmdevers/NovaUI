using NovaUI;
using NovaUI.Desktop;
using NovaUI.Widgets;

var count = new State<int>(0);

Desktop.Run(() =>
    new Padding(EdgeInsets.All(24),
        new Column(
            new Text($"Count: {count.Value}", 24),
            new Button("Increment", () => count.Value++))),
    title: "Counter");
