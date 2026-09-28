using System.Globalization;
using System.Text;

namespace Plugin.Maui.Spine.Barcodes;

/// <summary>
/// A barcode as modules: <see langword="true"/> is a dark module (a bar, or a lit lamp on a display that shows
/// the code as light). A linear code is one row high; draw it stretched to the height you want.
/// </summary>
public sealed class BarcodeMatrix
{
    private readonly bool[] _modules;

    public BarcodeMatrix(int width, int height, int quietZone = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(quietZone);
        Width = width;
        Height = height;
        QuietZone = quietZone;
        _modules = new bool[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The light modules to leave around the code when drawing it; not part of <see cref="Width"/> or <see cref="Height"/>.</summary>
    public int QuietZone { get; }

    /// <summary>A linear code: one row that is drawn as bars.</summary>
    public bool IsLinear => Height == 1;

    public bool this[int x, int y]
    {
        get => _modules[Index(x, y)];
        set => _modules[Index(x, y)] = value;
    }

    /// <summary>
    /// The code as an SVG document, one unit per module, quiet zone included. A linear code is drawn
    /// <paramref name="linearHeight"/> modules high.
    /// </summary>
    public string ToSvg(string foreground = "#000000", string background = "#FFFFFF", int linearHeight = 40)
    {
        int rows = IsLinear ? linearHeight : Height;
        int w = Width + 2 * QuietZone, h = rows + 2 * QuietZone;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" shape-rendering=\"crispEdges\">");
        if (!string.IsNullOrEmpty(background))
            svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{w}\" height=\"{h}\" fill=\"{background}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"{foreground}\" d=\"");
        // One horizontal run per path segment keeps the file small
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!this[x, y]) continue;
                int start = x;
                while (x + 1 < Width && this[x + 1, y]) x++;
                svg.Append(CultureInfo.InvariantCulture, $"M{start + QuietZone} {y + QuietZone}h{x - start + 1}v{(IsLinear ? rows : 1)}h-{x - start + 1}z");
            }
        }
        svg.Append("\"/></svg>");
        return svg.ToString();
    }

    public override string ToString()
    {
        var text = new StringBuilder();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++) text.Append(this[x, y] ? '#' : '.');
            text.AppendLine();
        }
        return text.ToString();
    }

    private int Index(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(null, $"Module ({x}, {y}) is outside the {Width} × {Height} matrix.");
        return y * Width + x;
    }
}
