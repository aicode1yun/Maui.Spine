using System.Numerics;

namespace Plugin.Maui.Spine.Barcodes;

/// <summary>
/// The grid of lights a <see cref="LightGridReader"/> looks for: a display whose code is its lamps, lit and unlit,
/// such as a 12 × 12 word clock showing a Data Matrix.
/// </summary>
/// <param name="Columns">Lamps across.</param>
/// <param name="Rows">Lamps down. Only square Data Matrix sizes (10, 12, 14 … 26) are read today.</param>
public sealed record LightGridOptions(int Columns = 12, int Rows = 12)
{
    /// <summary>The symbology the lamps show; only <see cref="BarcodeFormat.DataMatrix"/> today.</summary>
    public BarcodeFormat Format { get; init; } = BarcodeFormat.DataMatrix;
}

/// <summary>What <see cref="LightGridReader.Read"/> saw in one image.</summary>
public sealed class LightGridResult
{
    internal LightGridResult(string? text, IReadOnlyList<Vector2> corners, bool[,]? cells, string diagnostics)
    {
        Text = text;
        Corners = corners;
        Cells = cells;
        Diagnostics = diagnostics;
    }

    /// <summary>The decoded text, or <see langword="null"/> when no code was read.</summary>
    public string? Text { get; }

    public bool IsDecoded => Text is not null;

    /// <summary>
    /// The grid's outer corners in image pixels (top-left, top-right, bottom-right, bottom-left in grid order),
    /// or empty when no grid was found. The grid can be found without the code being read.
    /// </summary>
    public IReadOnlyList<Vector2> Corners { get; }

    /// <summary>Which lamps were taken as lit, [column, row], when a grid was found.</summary>
    public bool[,]? Cells { get; }

    /// <summary>The steps and their timings, for a log when a code is not read.</summary>
    public string Diagnostics { get; }
}

/// <summary>
/// Reads a matrix code shown on a grid of lights, such as a word clock whose letters are the modules.
/// Platform barcode readers expect filled squares; here a lit letter is thin strokes with a dark middle,
/// unlit letters may still be visible, and the glass reflects the room. The reader finds the grid first,
/// then measures each lamp by its brightest pixels.
/// </summary>
/// <remarks>
/// One instance keeps its working memory between images, so a live camera does not allocate per frame.
/// An instance is not thread-safe; give each camera its own. <see cref="Read"/> never throws for a bad
/// image: a frame it cannot make sense of is a miss, and <see cref="LightGridResult.Diagnostics"/> says why.
/// </remarks>
public sealed class LightGridReader
{
    private readonly LightGridEngine.Scratch _scratch = new();
    private byte[] _pixels = [];

    public LightGridReader(LightGridOptions? options = null)
    {
        Options = options ?? new LightGridOptions();
        if (Options.Format != BarcodeFormat.DataMatrix)
            throw new NotSupportedException($"A light grid can show a Data Matrix today, not {Options.Format}.");
        if (Options.Columns != Options.Rows || Options.Columns is < 10 or > 26 || Options.Columns % 2 != 0)
            throw new NotSupportedException($"A {Options.Columns} × {Options.Rows} light grid is not a square Data Matrix size (10, 12, 14 … 26).");
    }

    public LightGridOptions Options { get; }

    /// <summary>Reads one 8-bit luminance image, such as the Y plane of a camera frame.</summary>
    /// <param name="luminance">The pixels, row by row.</param>
    /// <param name="width">Pixels per row.</param>
    /// <param name="height">Rows.</param>
    /// <param name="stride">Bytes from one row to the next; at least <paramref name="width"/>.</param>
    public LightGridResult Read(ReadOnlySpan<byte> luminance, int width, int height, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width);
        if (luminance.Length < stride * (height - 1) + width)
            throw new ArgumentException($"{luminance.Length} bytes is less than a {width} × {height} image with stride {stride}.", nameof(luminance));

        if (_pixels.Length < width * height) _pixels = new byte[width * height];
        var packed = _pixels.AsSpan(0, width * height);
        if (stride == width)
            luminance[..packed.Length].CopyTo(packed);
        else
            for (int y = 0; y < height; y++)
                luminance.Slice(y * stride, width).CopyTo(packed.Slice(y * width, width));

        var read = LightGridEngine.Read(new LuminanceImage(_pixels, width, height), Options.Columns, Options.Rows, _scratch);
        IReadOnlyList<Vector2> corners = read.Grid is { } grid
            ? [Corner(grid, -0.5, -0.5), Corner(grid, Options.Columns - 0.5, -0.5), Corner(grid, Options.Columns - 0.5, Options.Rows - 0.5), Corner(grid, -0.5, Options.Rows - 0.5)]
            : [];
        return new LightGridResult(read.Text, corners, read.Lit, read.Log);

        static Vector2 Corner(Homography grid, double i, double j)
        {
            var p = grid.Map(i, j);
            return new Vector2((float)p.X, (float)p.Y);
        }
    }
}
