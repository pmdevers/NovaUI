using System.Globalization;
using NovaUI;
using NovaUI.Input;
using NovaUI.Rendering;
using NovaUI.Widgets;

sealed class CalculatorWidget : Widget
{
    private const float Padding = 16f;
    private const float Gap = 8f;
    private const float DisplayFontSize = 44f;
    private const float ButtonFontSize = 18f;

    private readonly ButtonSpec[] _keys =
    [
        new("C", () => { }, IsAccent: true),
        new("±", () => { }),
        new("%", () => { }),
        new("÷", () => { }, IsOperator: true),
        new("7", () => { }),
        new("8", () => { }),
        new("9", () => { }),
        new("×", () => { }, IsOperator: true),
        new("4", () => { }),
        new("5", () => { }),
        new("6", () => { }),
        new("−", () => { }, IsOperator: true),
        new("1", () => { }),
        new("2", () => { }),
        new("3", () => { }),
        new("+", () => { }, IsOperator: true),
        new("0", () => { }),
        new(".", () => { }),
        new("⌫", () => { }, IsAccent: true),
        new("=", () => { }, IsEquals: true),
    ];

    private readonly Rect[] _keyBounds;
    private readonly Size[] _keyLabelSizes;
    private Size _displayLabelSize;
    private Rect _displayBounds;
    private int _pressedKey = -1;
    private string _display = "0";
    private decimal _accumulator;
    private string? _pendingOp;
    private bool _startNewEntry = true;
    private bool _error;

    public CalculatorWidget()
    {
        _keyBounds = new Rect[_keys.Length];
        _keyLabelSizes = new Size[_keys.Length];
    }

    public override Size Layout(Constraints constraints, BuildContext context)
    {
        for (var i = 0; i < _keys.Length; i++)
            _keyLabelSizes[i] = context.Text.Measure(_keys[i].Label, ButtonFontSize);

        _displayLabelSize = context.Text.Measure(_display, DisplayFontSize);

        var width = float.IsPositiveInfinity(constraints.MaxWidth) ? 360f : constraints.MaxWidth;
        var height = float.IsPositiveInfinity(constraints.MaxHeight) ? 640f : constraints.MaxHeight;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        _displayBounds = new Rect(
            bounds.X + Padding,
            bounds.Y + Padding,
            bounds.Width - Padding * 2,
            Math.Max(96f, bounds.Height * 0.22f));

        var gridTop = _displayBounds.Bottom + Gap;
        var gridLeft = bounds.X + Padding;
        var gridWidth = bounds.Width - Padding * 2;
        var gridHeight = Math.Max(0, bounds.Bottom - gridTop - Padding);
        var cellWidth = (gridWidth - Gap * 3) / 4f;
        var cellHeight = (gridHeight - Gap * 4) / 5f;

        for (var index = 0; index < _keys.Length; index++)
        {
            var row = index / 4;
            var col = index % 4;
            var x = gridLeft + col * (cellWidth + Gap);
            var y = gridTop + row * (cellHeight + Gap);
            _keyBounds[index] = new Rect(x, y, cellWidth, cellHeight);
        }
    }

    public override void Paint(ICanvas canvas)
    {
        canvas.Clear(new Color(245, 246, 248));
        canvas.FillRect(Bounds, new Color(245, 246, 248));
        canvas.FillRect(_displayBounds, new Color(24, 28, 36), 16);

        var displayText = _display;
        var displayX = _displayBounds.Right - Padding - _displayLabelSize.Width;
        var displayY = _displayBounds.Y + (_displayBounds.Height + DisplayFontSize) / 2f - 8f;
        canvas.DrawText(displayText, new Point(Math.Max(_displayBounds.X + Padding, displayX), displayY), DisplayFontSize, Color.White);

        for (var i = 0; i < _keys.Length; i++)
        {
            var key = _keys[i];
            var bounds = _keyBounds[i];
            var pressed = i == _pressedKey;
            var background = key.IsEquals
                ? new Color(33, 150, 243)
                : key.IsOperator
                    ? new Color(255, 140, 0)
                    : key.IsAccent
                        ? new Color(90, 96, 110)
                        : new Color(224, 228, 235);
            if (pressed)
                background = new Color((byte)Math.Min(255, background.R + 18), (byte)Math.Min(255, background.G + 18), (byte)Math.Min(255, background.B + 18), background.A);

            var textColor = (key.IsEquals || key.IsOperator || key.IsAccent) ? Color.White : new Color(28, 32, 38);
            canvas.FillRect(bounds, background, 14);

            var labelSize = _keyLabelSizes[i];
            var labelX = bounds.X + (bounds.Width - labelSize.Width) / 2f;
            var labelY = bounds.Y + (bounds.Height + ButtonFontSize) / 2f - 6f;
            canvas.DrawText(key.Label, new Point(labelX, labelY), ButtonFontSize, textColor);
        }
    }

    public override bool HandlePointer(PointerEvent e)
    {
        var hit = HitTest(e.Position);

        switch (e.Kind)
        {
            case PointerKind.Down:
                _pressedKey = hit;
                return hit >= 0;

            case PointerKind.Up:
                var pressed = _pressedKey;
                _pressedKey = -1;
                if (pressed < 0)
                    return false;

                if (pressed == hit)
                    Press(_keys[pressed].Label);

                return true;

            default:
                return false;
        }
    }

    private int HitTest(Point point)
    {
        for (var i = 0; i < _keyBounds.Length; i++)
            if (_keyBounds[i].Contains(point))
                return i;

        return -1;
    }

    private void Press(string key)
    {
        if (key == "C")
        {
            ClearAll();
            return;
        }

        if (_error)
        {
            if (char.IsDigit(key[0]) || key == ".")
                ClearAll();
            else
                return;
        }

        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            AppendDigit(key[0]);
            return;
        }

        switch (key)
        {
            case ".":
                AppendDecimalPoint();
                break;
            case "±":
                ToggleSign();
                break;
            case "%":
                ApplyPercent();
                break;
            case "⌫":
                Backspace();
                break;
            case "÷":
            case "×":
            case "−":
            case "+":
                ApplyOperator(key);
                break;
            case "=":
                Evaluate();
                break;
        }
    }

    private void AppendDigit(char digit)
    {
        if (_startNewEntry || _display == "0")
        {
            _display = digit.ToString();
            _startNewEntry = false;
            return;
        }

        _display += digit;
    }

    private void AppendDecimalPoint()
    {
        if (_startNewEntry)
        {
            _display = "0.";
            _startNewEntry = false;
            return;
        }

        if (!_display.Contains('.'))
            _display += '.';
    }

    private void ToggleSign()
    {
        if (_display == "0") return;
        _display = _display.StartsWith('-') ? _display[1..] : "-" + _display;
        _startNewEntry = false;
    }

    private void ApplyPercent()
    {
        if (!TryGetCurrentValue(out var value)) return;
        _display = Format(value / 100m);
        _startNewEntry = true;
    }

    private void Backspace()
    {
        if (_startNewEntry)
            return;

        if (_display.Length <= 1 || (_display.Length == 2 && _display.StartsWith('-')))
        {
            _display = "0";
            _startNewEntry = true;
            return;
        }

        _display = _display[..^1];
    }

    private void ApplyOperator(string op)
    {
        if (!TryGetCurrentValue(out var current)) return;

        if (_pendingOp is null)
        {
            _accumulator = current;
            _pendingOp = op;
            _startNewEntry = true;
            return;
        }

        if (!_startNewEntry)
        {
            if (!TryApply(_accumulator, current, _pendingOp, out var result))
                return;

            _accumulator = result;
            _display = Format(result);
        }

        _pendingOp = op;
        _startNewEntry = true;
    }

    private void Evaluate()
    {
        if (_pendingOp is null)
        {
            _startNewEntry = true;
            return;
        }

        if (!TryGetCurrentValue(out var current)) return;
        if (!TryApply(_accumulator, current, _pendingOp, out var result)) return;

        _display = Format(result);
        _accumulator = result;
        _pendingOp = null;
        _startNewEntry = true;
    }

    private bool TryGetCurrentValue(out decimal value)
    {
        return decimal.TryParse(_display, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private bool TryApply(decimal left, decimal right, string op, out decimal result)
    {
        try
        {
            result = op switch
            {
                "+" => left + right,
                "−" => left - right,
                "×" => left * right,
                "÷" => right == 0 ? throw new DivideByZeroException() : left / right,
                _ => right,
            };

            return true;
        }
        catch
        {
            _display = "Error";
            _error = true;
            _pendingOp = null;
            _startNewEntry = true;
            _accumulator = 0;
            result = 0;
            return false;
        }
    }

    private void ClearAll()
    {
        _display = "0";
        _accumulator = 0;
        _pendingOp = null;
        _startNewEntry = true;
        _error = false;
    }

    private static string Format(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Contains('.'))
            text = text.TrimEnd('0').TrimEnd('.');

        return string.IsNullOrWhiteSpace(text) || text == "-0" ? "0" : text;
    }

    private sealed record ButtonSpec(string Label, Action Pressed, bool IsAccent = false, bool IsOperator = false, bool IsEquals = false);
}
