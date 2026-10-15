namespace NovaUI;

public readonly record struct Size(float Width, float Height)
{
    public static readonly Size Zero = new(0, 0);
}

public readonly record struct Point(float X, float Y);

public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool Contains(Point p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
}

public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Blue = new(33, 150, 243);
}

public readonly record struct EdgeInsets(float Left, float Top, float Right, float Bottom)
{
    public static EdgeInsets All(float v) => new(v, v, v, v);
    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;
}

/// <summary>Min/max size a widget may take, passed down during layout.</summary>
public readonly record struct Constraints(float MinWidth, float MaxWidth, float MinHeight, float MaxHeight)
{
    public static Constraints Tight(Size s) => new(s.Width, s.Width, s.Height, s.Height);
    public static Constraints Loose(Size s) => new(0, s.Width, 0, s.Height);

    public Size Constrain(Size s) => new(
        Math.Clamp(s.Width, MinWidth, MaxWidth),
        Math.Clamp(s.Height, MinHeight, MaxHeight));

    public Constraints Deflate(EdgeInsets e) => new(
        Math.Max(0, MinWidth - e.Horizontal), Math.Max(0, MaxWidth - e.Horizontal),
        Math.Max(0, MinHeight - e.Vertical), Math.Max(0, MaxHeight - e.Vertical));
}
