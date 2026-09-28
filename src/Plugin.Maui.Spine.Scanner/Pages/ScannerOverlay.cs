using Plugin.Maui.Spine.Barcodes;

namespace Plugin.Maui.Spine.Scanner;

/// <summary>The outline <see cref="ScannerOverlay"/> draws to show where to aim.</summary>
internal enum ReticleShape
{
    None,

    /// <summary>For 2D codes and light grids.</summary>
    Square,

    /// <summary>For linear codes such as EAN.</summary>
    Wide,
}

/// <summary>
/// Drawn over the camera in <see cref="BarcodeScannerPage"/>: the pulsing corners of the area to aim at, and after a
/// hit the code that was read, outlined in the accent colour with its lamp grid for a light grid.
/// </summary>
internal sealed class ScannerOverlay : GraphicsView, IDrawable
{
    private static readonly Color FallbackAccent = Color.FromArgb("#0A84FF");

    public ScannerOverlay()
    {
        Drawable = this;
        InputTransparent = true;
        BackgroundColor = Colors.Transparent;
    }

    public ReticleShape Reticle { get; set; } = ReticleShape.Square;

    /// <summary>0 to 1, driven by the page's pulse animation.</summary>
    public double Pulse { get; set; }

    /// <summary>The code to mark, or <see langword="null"/> while scanning.</summary>
    public BarcodeScanResult? Hit { get; set; }

    /// <summary>0 to 1: how far the marking has faded in.</summary>
    public double HitProgress { get; set; }

    private static Color Accent =>
        Application.Current?.Resources.TryGetValue("Accent", out var value) == true && value is Color color ? color : FallbackAccent;

    public void Draw(ICanvas canvas, RectF rect)
    {
        var accent = Accent;
        if (Hit is { Corners.Count: 4 } hit)
            DrawHit(canvas, hit, accent);
        else if (Reticle != ReticleShape.None && Hit is null)
            DrawReticle(canvas, rect, accent);
    }

    /// <summary>Where the aim corners go in a view of the given size; the page places its prompt below it.</summary>
    public static RectF ReticleFor(RectF rect, ReticleShape shape)
    {
        // Half the header's height lower than the sheet's centre: centred in the sheet it sits high under the
        // buttons, centred below the header it looks low
        float header = (float)(Plugin.Maui.Spine.Presentation.HeaderBarConstants.SheetTopPadding + Plugin.Maui.Spine.Presentation.HeaderBarConstants.BarHeight);
        rect = new RectF(rect.X, rect.Y + header / 2, rect.Width, Math.Max(0, rect.Height - header / 2));
        float width, height;
        if (shape == ReticleShape.Wide)
        {
            width = Math.Min(rect.Width * 0.82f, 420);
            height = width * 0.42f;
        }
        else
        {
            width = height = Math.Min(Math.Min(rect.Width, rect.Height) * 0.62f, 320);
        }
        return new RectF(rect.Center.X - width / 2, rect.Center.Y - height / 2, width, height);
    }

    private void DrawReticle(ICanvas canvas, RectF rect, Color accent)
    {
        var frame = ReticleFor(rect, Reticle);
        float left = frame.Left, top = frame.Top, width = frame.Width, height = frame.Height;
        float arm = Math.Min(width, height) * 0.2f, radius = arm * 0.35f;

        canvas.StrokeColor = accent.WithAlpha(0.35f + 0.65f * (float)Pulse);
        canvas.StrokeSize = 5;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.Antialias = true;
        foreach (var (x, y, dx, dy) in new[] { (left, top, 1f, 1f), (left + width, top, -1f, 1f), (left + width, top + height, -1f, -1f), (left, top + height, 1f, -1f) })
        {
            var corner = new PathF();
            corner.MoveTo(x, y + dy * arm);
            corner.LineTo(x, y + dy * radius);
            corner.QuadTo(x, y, x + dx * radius, y);
            corner.LineTo(x + dx * arm, y);
            canvas.DrawPath(corner);
        }
    }

    private void DrawHit(ICanvas canvas, BarcodeScanResult hit, Color accent)
    {
        float t = (float)HitProgress;
        var quad = new QuadMap(hit.Corners.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray());
        canvas.Antialias = true;

        // The code redrawn from what was read, a dot per dark module, in the perspective it was found in
        if (_modules is { } modules)
        {
            int w = modules.GetLength(0), h = modules.GetLength(1);
            canvas.FillColor = accent.WithAlpha(t);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!modules[x, y]) continue;
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    var centre = quad.Map(u, v);
                    if (h == 1)
                    {
                        // A linear code: its bars, the full height of the code
                        var top = quad.Map(u, 0.08f);
                        var bottom = quad.Map(u, 0.92f);
                        canvas.StrokeColor = accent.WithAlpha(t);
                        canvas.StrokeSize = Math.Max(1.5f, Distance(quad.Map(x / (float)w, 0.5f), quad.Map((x + 1f) / w, 0.5f)) * 0.8f);
                        canvas.StrokeLineCap = LineCap.Round;
                        canvas.DrawLine(top, bottom);
                        continue;
                    }
                    // Each dot fills most of its module, measured where it lands, so the far side stays smaller
                    float across = Distance(quad.Map(x / (float)w, v), quad.Map((x + 1f) / w, v));
                    float down = Distance(quad.Map(u, y / (float)h), quad.Map(u, (y + 1f) / h));
                    canvas.FillCircle(centre, Math.Min(across, down) * 0.42f);
                }
            return;
        }

        // Nothing to redraw: the code's outline
        var outline = new PathF();
        outline.MoveTo(quad.Map(0, 0));
        outline.LineTo(quad.Map(1, 0));
        outline.LineTo(quad.Map(1, 1));
        outline.LineTo(quad.Map(0, 1));
        outline.Close();
        canvas.FillColor = accent.WithAlpha(0.28f * t);
        canvas.FillPath(outline);
        canvas.StrokeColor = accent.WithAlpha(t);
        canvas.StrokeSize = 4;
        canvas.DrawPath(outline);
    }

    private bool[,]? _modules;

    /// <summary>
    /// The modules to redraw for a hit: the code encoded again from its value. For a light grid it is turned to match
    /// the lamps that were lit, so each dot lands on its lamp; a 2D code from the platform reader is encoded with
    /// default options, so its pattern may differ in detail from the one on screen.
    /// </summary>
    public static bool[,]? ModulesFor(BarcodeScanResult hit)
    {
        try
        {
            if (hit.Grid is { } grid)
            {
                var matrix = Barcode.Encode(hit.Value, new DataMatrixOptions { Size = (grid.Columns, grid.Rows) });
                return hit.Cells is { } cells ? BestTurn(matrix, cells) : ToArray(matrix);
            }
            return ToArray(Barcode.Encode(hit.Value, hit.Format));
        }
        catch (Exception ex) when (ex is BarcodeEncodingException or ArgumentException)
        {
            return null;
        }
    }

    public void ShowHit(BarcodeScanResult hit)
    {
        Hit = hit;
        _modules = hit.Corners.Count == 4 ? ModulesFor(hit) : null;
    }

    private static bool[,] ToArray(BarcodeMatrix m)
    {
        var a = new bool[m.Width, m.Height];
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
                a[x, y] = m[x, y];
        return a;
    }

    // The reader may have seen the code turned or mirrored; of the eight ways to lay the matrix down, take the one
    // that agrees with most lamps
    private static bool[,] BestTurn(BarcodeMatrix m, bool[,] lit)
    {
        int n = m.Width;
        bool[,]? best = null;
        int bestScore = -1;
        foreach (bool mirror in new[] { false, true })
            for (int turn = 0; turn < 4; turn++)
            {
                var a = new bool[n, n];
                int score = 0;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int sx = mirror ? n - 1 - x : x, sy = y;
                        for (int r = 0; r < turn; r++) (sx, sy) = (sy, n - 1 - sx);
                        a[x, y] = m[sx, sy];
                        if (x < lit.GetLength(0) && y < lit.GetLength(1) && a[x, y] == lit[x, y]) score++;
                    }
                if (score > bestScore) (best, bestScore) = (a, score);
            }
        return best!;
    }

    private static float Distance(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>The unit square to the code's four corners, with perspective (Heckbert's square-to-quad mapping).</summary>
    private readonly struct QuadMap
    {
        private readonly float _a, _b, _c, _d, _e, _f, _g, _h;

        public QuadMap(PointF[] q)
        {
            float x0 = q[0].X, y0 = q[0].Y, x1 = q[1].X, y1 = q[1].Y, x2 = q[2].X, y2 = q[2].Y, x3 = q[3].X, y3 = q[3].Y;
            float sx = x0 - x1 + x2 - x3, sy = y0 - y1 + y2 - y3;
            float dx1 = x1 - x2, dx2 = x3 - x2, dy1 = y1 - y2, dy2 = y3 - y2;
            float den = dx1 * dy2 - dx2 * dy1;
            _g = den == 0 ? 0 : (sx * dy2 - dx2 * sy) / den;
            _h = den == 0 ? 0 : (dx1 * sy - sx * dy1) / den;
            _a = x1 - x0 + _g * x1; _b = x3 - x0 + _h * x3; _c = x0;
            _d = y1 - y0 + _g * y1; _e = y3 - y0 + _h * y3; _f = y0;
        }

        public PointF Map(float u, float v)
        {
            float w = _g * u + _h * v + 1;
            return new PointF((_a * u + _b * v + _c) / w, (_d * u + _e * v + _f) / w);
        }
    }

}
