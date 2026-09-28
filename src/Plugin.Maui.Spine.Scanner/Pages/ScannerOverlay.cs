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

    private void DrawReticle(ICanvas canvas, RectF rect, Color accent)
    {
        // Half the header's height lower than the sheet's centre: centred in the sheet it sits high under the
        // buttons, centred below the header it looks low
        float header = (float)(Plugin.Maui.Spine.Presentation.HeaderBarConstants.SheetTopPadding + Plugin.Maui.Spine.Presentation.HeaderBarConstants.BarHeight);
        rect = new RectF(rect.X, rect.Y + header / 2, rect.Width, Math.Max(0, rect.Height - header / 2));
        float width, height;
        if (Reticle == ReticleShape.Wide)
        {
            width = Math.Min(rect.Width * 0.82f, 420);
            height = width * 0.42f;
        }
        else
        {
            width = height = Math.Min(Math.Min(rect.Width, rect.Height) * 0.62f, 320);
        }
        float left = rect.Center.X - width / 2, top = rect.Center.Y - height / 2;
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
        var c = hit.Corners.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
        var outline = new PathF();
        outline.MoveTo(c[0]);
        for (int i = 1; i < 4; i++) outline.LineTo(c[i]);
        outline.Close();

        canvas.Antialias = true;
        canvas.FillColor = accent.WithAlpha(0.28f * t);
        canvas.FillPath(outline);

        // A light grid shows its lamps: lines between the corners, as the reader found them
        if (hit.Grid is { } grid)
        {
            canvas.StrokeColor = accent.WithAlpha(0.7f * t);
            canvas.StrokeSize = 1.2f;
            for (int i = 1; i < grid.Columns; i++)
                canvas.DrawLine(Lerp(c[0], c[1], i / (float)grid.Columns), Lerp(c[3], c[2], i / (float)grid.Columns));
            for (int j = 1; j < grid.Rows; j++)
                canvas.DrawLine(Lerp(c[0], c[3], j / (float)grid.Rows), Lerp(c[1], c[2], j / (float)grid.Rows));
        }

        canvas.StrokeColor = accent.WithAlpha(t);
        canvas.StrokeSize = 4;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(outline);
    }

    private static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
