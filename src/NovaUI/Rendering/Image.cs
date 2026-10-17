namespace NovaUI.Rendering;

/// <summary>Immutable bitmap with straight-alpha RGBA pixels (4 bytes per pixel, row-major).</summary>
public sealed class Image
{
    public Image(int width, int height, byte[] rgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("Pixel buffer must be width * height * 4 bytes.", nameof(rgba));
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Rgba { get; }

    public Color GetPixel(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return new Color(Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
    }

    /// <summary>Builds an image by evaluating <paramref name="pixel"/> for every coordinate.</summary>
    public static Image Create(int width, int height, Func<int, int, Color> pixel)
    {
        var data = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var c = pixel(x, y);
                var i = (y * width + x) * 4;
                data[i] = c.R; data[i + 1] = c.G; data[i + 2] = c.B; data[i + 3] = c.A;
            }
        return new Image(width, height, data);
    }
}
