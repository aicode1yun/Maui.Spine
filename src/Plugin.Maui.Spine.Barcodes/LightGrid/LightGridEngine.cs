using ZXing.Common;

namespace Plugin.Maui.Spine.Barcodes;

internal readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
}

/// <summary>Grid coordinates (column, row) to image coordinates.</summary>
internal sealed class Homography
{
    readonly double[] h;
    Homography(double[] h) => this.h = h;

    public Vec2 Map(double gx, double gy)
    {
        double w = h[6] * gx + h[7] * gy + 1;
        return new((h[0] * gx + h[1] * gy + h[2]) / w, (h[3] * gx + h[4] * gy + h[5]) / w);
    }

    public Vec2 Unmap(Vec2 p)
    {
        // Inverse of [h0 h1 h2; h3 h4 h5; h6 h7 1]
        double a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], k = h[7];
        double A = e - f * k, B = c * k - b, C = b * f - c * e;
        double D = f * g - d, E = a - c * g, F = c * d - a * f;
        double G = d * k - e * g, H = b * g - a * k, I = a * e - b * d;
        double w = G * p.X + H * p.Y + I;
        return new((A * p.X + B * p.Y + C) / w, (D * p.X + E * p.Y + F) / w);
    }

    public Homography Offset(double di, double dj)
    {
        // Grid coordinate (i, j) of the result is (i + di, j + dj) of this one
        var n = (double[])h.Clone();
        n[2] = h[0] * di + h[1] * dj + h[2];
        n[5] = h[3] * di + h[4] * dj + h[5];
        double w = h[6] * di + h[7] * dj + 1;
        for (int i = 0; i < 8; i++) n[i] /= w;
        return new Homography(n);
    }

    public Homography Scale(double s)
    {
        var n = (double[])h.Clone();
        for (int i = 0; i < 6; i++) n[i] *= s;
        return new Homography(n);
    }

    public static Homography? Fit(IReadOnlyList<(Vec2 Grid, Vec2 Image)> pairs)
    {
        if (pairs.Count < 4) return null;
        double mx = pairs.Average(p => p.Image.X), my = pairs.Average(p => p.Image.Y);
        double s = pairs.Average(p => (p.Image - new Vec2(mx, my)).Length);
        if (s <= 0) return null;

        var ata = new double[8, 8];
        var atb = new double[8];
        foreach (var (gp, ip) in pairs)
        {
            double X = gp.X, Y = gp.Y, x = (ip.X - mx) / s, y = (ip.Y - my) / s;
            Accumulate([X, Y, 1, 0, 0, 0, -X * x, -Y * x], x);
            Accumulate([0, 0, 0, X, Y, 1, -X * y, -Y * y], y);
        }
        var sol = Solve(ata, atb);
        if (sol is null) return null;

        // Undo the normalisation: image = s * n + m
        var r = new double[8];
        r[0] = s * sol[0] + mx * sol[6]; r[1] = s * sol[1] + mx * sol[7]; r[2] = s * sol[2] + mx;
        r[3] = s * sol[3] + my * sol[6]; r[4] = s * sol[4] + my * sol[7]; r[5] = s * sol[5] + my;
        r[6] = sol[6]; r[7] = sol[7];
        return new Homography(r);

        void Accumulate(double[] row, double rhs)
        {
            for (int i = 0; i < 8; i++)
            {
                atb[i] += row[i] * rhs;
                for (int j = 0; j < 8; j++) ata[i, j] += row[i] * row[j];
            }
        }
    }

    static double[]? Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = new double[n, n + 1];
        for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) m[i, j] = a[i, j]; m[i, n] = b[i]; }
        for (int c = 0; c < n; c++)
        {
            int p = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[p, c])) p = r;
            if (Math.Abs(m[p, c]) < 1e-12) return null;
            for (int j = 0; j <= n; j++) (m[c, j], m[p, j]) = (m[p, j], m[c, j]);
            for (int r = 0; r < n; r++)
            {
                if (r == c) continue;
                double f = m[r, c] / m[c, c];
                for (int j = c; j <= n; j++) m[r, j] -= f * m[c, j];
            }
        }
        var x = new double[n];
        for (int i = 0; i < n; i++) x[i] = m[i, n] / m[i, i];
        return x;
    }
}

internal sealed class LuminanceImage(byte[] pixels, int width, int height)
{
    public byte[] Pixels { get; } = pixels;
    public int Width { get; } = width;
    public int Height { get; } = height;

    public double Sample(double x, double y)
    {
        if (x < 0 || y < 0 || x >= Width - 1 || y >= Height - 1) return 0;
        int x0 = (int)x, y0 = (int)y;
        double fx = x - x0, fy = y - y0;
        int i = y0 * Width + x0;
        double top = Pixels[i] * (1 - fx) + Pixels[i + 1] * fx;
        double bottom = Pixels[i + Width] * (1 - fx) + Pixels[i + Width + 1] * fx;
        return top * (1 - fy) + bottom * fy;
    }
}

internal sealed record GridRead(
    string? Text,
    Homography? Grid,
    double[,]? Brightness,
    bool[,]? Lit,
    IReadOnlyList<Vec2> Blobs,
    IReadOnlyList<Vec2> Assigned,
    string Log);

/// <summary>
/// The steps behind <see cref="LightGridReader"/>: find the lights, grow and fit the grid, measure each cell by
/// its brightest pixels and decode the resulting matrix.
/// </summary>
internal static class LightGridEngine
{
    public static GridRead Read(LuminanceImage img, int columns = 12, int rows = 12, Scratch? scratch = null)
    {
        var log = new System.Text.StringBuilder();
        try
        {
            return ReadCore(img, columns, rows, scratch ?? new Scratch(), log);
        }
        catch (Exception ex)
        {
            // A frame the reader cannot make sense of is a miss, not a crash; the log keeps the cause
            log.AppendLine($"ERROR {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return new(null, null, null, null, [], [], log.ToString());
        }
    }

    static GridRead ReadCore(LuminanceImage img, int columns, int rows, Scratch scratch, System.Text.StringBuilder log)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var (blobs, letterHeight) = FindBlobs(img, log, scratch);
        log.AppendLine($"t blobs {clock.Elapsed.TotalMilliseconds:F1} ms");
        log.AppendLine($"blobs: {blobs.Count}");
        if (blobs.Count < 8 || blobs.Count > 4000) return new(null, null, null, null, blobs, [], log.ToString());

        // Letter pitch is 1.5–2.5 letter heights; buckets that size keep neighbour search linear on busy frames
        var index = new SpatialIndex(blobs, 3.5 * letterHeight);
        var (u, v) = EstimateBasis(blobs, index, 3.5 * letterHeight);
        log.AppendLine($"basis u=({u.X:F1},{u.Y:F1}) v=({v.X:F1},{v.Y:F1})");
        if (u.Length < 2 || v.Length < 2) return new(null, null, null, null, blobs, [], log.ToString());

        var coords = GrowLattice(blobs, index, u, v);
        log.AppendLine($"t lattice {clock.Elapsed.TotalMilliseconds:F1} ms");
        log.AppendLine($"assigned: {coords.Count}");
        var pairs = coords.Select(kv => (new Vec2(kv.Value.I, kv.Value.J), blobs[kv.Key])).ToList();
        var h = Homography.Fit(pairs);
        if (h is null) return new(null, null, null, null, blobs, [], log.ToString());

        // Refit without outliers, then re-index every blob through the fitted grid
        double spacing = Math.Min(u.Length, v.Length);
        for (int pass = 0; pass < 2; pass++)
        {
            var kept = new List<(Vec2, Vec2)>();
            foreach (var b in blobs)
            {
                var g = h.Unmap(b);
                var gi = new Vec2(Math.Round(g.X), Math.Round(g.Y));
                if ((h.Map(gi.X, gi.Y) - b).Length < 0.3 * spacing) kept.Add((gi, b));
            }
            h = Homography.Fit(kept) ?? h;
        }

        // Only look near the lattice that was grown: perspective sends far-away specks to huge grid
        // coordinates, and an unbounded extent made a busy frame allocate tens of megabytes
        int reach = 3 * Math.Max(columns, rows);
        int loI = Math.Max(-reach, coords.Values.Min(c => c.I) - columns), hiI = Math.Min(reach, coords.Values.Max(c => c.I) + columns);
        int loJ = Math.Max(-reach, coords.Values.Min(c => c.J) - rows), hiJ = Math.Min(reach, coords.Values.Max(c => c.J) + rows);
        var cells = new Dictionary<(int, int), int>();
        var assigned = new List<Vec2>();
        foreach (var b in blobs)
        {
            var g = h.Unmap(b);
            if (!(g.X >= loI - 0.5 && g.X <= hiI + 0.5 && g.Y >= loJ - 0.5 && g.Y <= hiJ + 0.5)) continue;
            int gi = (int)Math.Round(g.X), gj = (int)Math.Round(g.Y);
            if ((h.Map(gi, gj) - b).Length < 0.3 * spacing)
            {
                cells[(gi, gj)] = cells.GetValueOrDefault((gi, gj)) + 1;
                assigned.Add(b);
            }
        }
        log.AppendLine($"on grid: {assigned.Count} in {cells.Count} cells");
        if (cells.Count == 0) return new(null, h, null, null, blobs, assigned, log.ToString());

        int imin = cells.Keys.Min(k => k.Item1), imax = cells.Keys.Max(k => k.Item1);
        int jmin = cells.Keys.Min(k => k.Item2), jmax = cells.Keys.Max(k => k.Item2);
        log.AppendLine($"extent: {imax - imin + 1} x {jmax - jmin + 1}");

        var windows = Windows(cells, imin, imax, jmin, jmax, columns, rows).Take(6).ToList();
        log.AppendLine($"t windows {clock.Elapsed.TotalMilliseconds:F1} ms");
        GridRead? best = null;
        foreach (var (i0, j0) in windows)
        {
            var grid = h.Offset(i0, j0);
            var brightness = Measure(img, grid, columns, rows);
            var (text, lit) = Decode(brightness, columns, rows);
            var result = new GridRead(text, grid, brightness, lit, blobs, assigned, log.ToString());
            if (text is not null)
            {
                log.AppendLine($"decoded at window ({i0},{j0}), t {clock.Elapsed.TotalMilliseconds:F1} ms");
                return result with { Log = log.ToString() };
            }
            best ??= result;
        }
        log.AppendLine($"no decode, t {clock.Elapsed.TotalMilliseconds:F1} ms");
        return (best ?? new(null, h, null, null, blobs, assigned, "")) with { Log = log.ToString() };
    }

    static IEnumerable<(int, int)> Windows(Dictionary<(int, int), int> cells, int imin, int imax, int jmin, int jmax, int columns, int rows)
    {
        // Summed-area table over the extent, so a busy frame with a huge extent stays cheap
        int w = imax - imin + 1, h = jmax - jmin + 1;
        var sat = new int[(w + 1) * (h + 1)];
        foreach (var ((i, j), n) in cells) sat[(j - jmin + 1) * (w + 1) + (i - imin + 1)] += n;
        for (int y = 1; y <= h; y++)
            for (int x = 1; x <= w; x++)
                sat[y * (w + 1) + x] += sat[(y - 1) * (w + 1) + x] + sat[y * (w + 1) + x - 1] - sat[(y - 1) * (w + 1) + x - 1];
        int Sum(int x0, int y0, int x1, int y1)
        {
            x0 = Math.Clamp(x0, 0, w); x1 = Math.Clamp(x1, 0, w); y0 = Math.Clamp(y0, 0, h); y1 = Math.Clamp(y1, 0, h);
            return sat[y1 * (w + 1) + x1] - sat[y0 * (w + 1) + x1] - sat[y1 * (w + 1) + x0] + sat[y0 * (w + 1) + x0];
        }

        var list = new List<(int I, int J, int Count)>();
        for (int i0 = Math.Min(imin, imax - columns + 1); i0 <= Math.Max(imin, imax - columns + 1); i0++)
            for (int j0 = Math.Min(jmin, jmax - rows + 1); j0 <= Math.Max(jmin, jmax - rows + 1); j0++)
                list.Add((i0, j0, Sum(i0 - imin, j0 - jmin, i0 - imin + columns, j0 - jmin + rows)));
        return list.OrderByDescending(w => w.Count).Select(w => (w.I, w.J));
    }

    /// <summary>
    /// Working memory for one reader at a time. A live camera keeps one so it does not allocate ~6 MB per frame;
    /// it must not be per-thread, because a dispatch queue hops between pool threads.
    /// </summary>
    internal sealed class Scratch
    {
        internal int[]? Integral;
        internal int[]? Stack;
        internal bool[]? Mask, Seen;
    }

    static (List<Vec2> Blobs, double LetterHeight) FindBlobs(LuminanceImage img, System.Text.StringBuilder log, Scratch scratch)
    {
        int w = img.Width, h = img.Height, stride = w + 1;
        ReadOnlySpan<byte> px = img.Pixels.AsSpan(0, w * h);
        // Reused between frames: a live camera would otherwise allocate ~6 MB per frame
        Span<int> integral = Buffer(ref scratch.Integral, stride * (h + 1)).AsSpan(0, stride * (h + 1));
        integral[..stride].Clear();
        for (int y = 0; y < h; y++)
        {
            var src = px.Slice(y * w, w);
            var above = integral.Slice(y * stride, stride);
            var row = integral.Slice((y + 1) * stride, stride);
            int run = 0;
            row[0] = 0;
            for (int x = 0; x < src.Length; x++)
            {
                run += src[x];
                row[x + 1] = above[x + 1] + run;
            }
        }

        // Adaptive threshold in integers: pixel * area > sum + 18 * area is pixel > local mean + 18
        int r = Math.Max(4, Math.Min(w, h) / 40);
        Span<bool> mask = Buffer(ref scratch.Mask, w * h).AsSpan(0, w * h);
        for (int y = 0; y < h; y++)
        {
            int y0 = Math.Max(0, y - r), y1 = Math.Min(h, y + r + 1);
            var top = integral.Slice(y0 * stride, stride);
            var bottom = integral.Slice(y1 * stride, stride);
            var src = px.Slice(y * w, w);
            var dst = mask.Slice(y * w, w);
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r), x1 = Math.Min(w, x + r + 1);
                int area = (x1 - x0) * (y1 - y0);
                int sum = bottom[x1] - top[x1] - bottom[x0] + top[x0];
                int p = src[x];
                dst[x] = p > 40 && p * area > sum + 18 * area;
            }
        }

        var comps = new List<(int Area, int MinX, int MinY, int MaxX, int MaxY)>();
        Span<bool> seen = Buffer(ref scratch.Seen, w * h).AsSpan(0, w * h);
        seen.Clear();
        // Every pixel is pushed at most once, so a stack the size of the image never overflows
        Span<int> stack = Buffer(ref scratch.Stack, w * h).AsSpan(0, w * h);
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || seen[start]) continue;
            int area = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0, top = 0;
            stack[top++] = start; seen[start] = true;
            while (top > 0)
            {
                int p = stack[--top];
                int x = p % w, y = p / w;
                area++;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                int ya = Math.Max(0, y - 1), yb = Math.Min(h - 1, y + 1), xa = Math.Max(0, x - 1), xb = Math.Min(w - 1, x + 1);
                for (int ny = ya; ny <= yb; ny++)
                    for (int nx = xa; nx <= xb; nx++)
                    {
                        int q = ny * w + nx;
                        if (mask[q] && !seen[q]) { seen[q] = true; stack[top++] = q; }
                    }
            }
            comps.Add((area, minX, minY, maxX, maxY));
        }

        int maxSize = Math.Min(w, h) / 8;
        var candidates = comps.Where(c => c.Area >= 8 && c.MaxX - c.MinX < maxSize && c.MaxY - c.MinY < maxSize).ToList();
        if (candidates.Count == 0) return ([], 0);
        var heights = candidates.Select(c => c.MaxY - c.MinY + 1).Order().ToList();
        // Letters are the most common tall-ish component; take the median of the upper half to skip specks
        double hMed = heights[heights.Count * 3 / 4];
        log.AppendLine($"components: {comps.Count}, candidates: {candidates.Count}, letter height≈{hMed}");
        var blobs = candidates
            .Where(c => { int ch = c.MaxY - c.MinY + 1, cw = c.MaxX - c.MinX + 1; return ch >= 0.5 * hMed && ch <= 1.8 * hMed && cw <= 2.0 * hMed; })
            .Select(c => new Vec2((c.MinX + c.MaxX) / 2.0, (c.MinY + c.MaxY) / 2.0))
            .ToList();
        return (blobs, hMed);

        static T[] Buffer<T>(ref T[]? buffer, int length)
        {
            if (buffer is null || buffer.Length < length) buffer = new T[length];
            return buffer;
        }
    }

    static (Vec2 U, Vec2 V) EstimateBasis(List<Vec2> pts, SpatialIndex index, double radius)
    {
        var nn = new List<double>();
        var vectors = new List<Vec2>();
        foreach (var p in pts)
        {
            var near = index.Near(p, radius).Select(i => pts[i]).Where(q => q != p).OrderBy(q => (q - p).Length).Take(4).ToList();
            if (near.Count == 0) continue;
            nn.Add((near[0] - p).Length);
            vectors.AddRange(near.Select(q => q - p));
        }
        if (nn.Count == 0) return (default, default);
        double d = nn.Order().ElementAt(nn.Count / 2);
        var horizontal = new List<Vec2>();
        var vertical = new List<Vec2>();
        foreach (var vec in vectors.Where(x => x.Length < 1.35 * d && x.Length > 0.6 * d))
        {
            if (Math.Abs(vec.X) >= Math.Abs(vec.Y)) horizontal.Add(vec.X < 0 ? vec * -1 : vec);
            else vertical.Add(vec.Y < 0 ? vec * -1 : vec);
        }
        return (Median(horizontal), Median(vertical));

        static Vec2 Median(List<Vec2> v) => v.Count == 0 ? default :
            new(v.Select(p => p.X).Order().ElementAt(v.Count / 2), v.Select(p => p.Y).Order().ElementAt(v.Count / 2));
    }

    static Dictionary<int, (int I, int J)> GrowLattice(List<Vec2> pts, SpatialIndex index, Vec2 u, Vec2 v)
    {
        var centre = new Vec2(pts.Select(p => p.X).Order().ElementAt(pts.Count / 2), pts.Select(p => p.Y).Order().ElementAt(pts.Count / 2));
        int seed = Enumerable.Range(0, pts.Count).MinBy(i => (pts[i] - centre).Length);
        var coords = new Dictionary<int, (int I, int J)> { [seed] = (0, 0) };
        var taken = new HashSet<(int, int)> { (0, 0) };
        var local = new Dictionary<int, (Vec2 U, Vec2 V)> { [seed] = (u, v) };
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        (int, int)[] steps = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1), (2, 0), (-2, 0), (0, 2), (0, -2)];
        while (queue.Count > 0)
        {
            int b = queue.Dequeue();
            var (bu, bv) = local[b];
            double tol = 0.3 * Math.Min(bu.Length, bv.Length);
            foreach (var (di, dj) in steps)
            {
                var target = pts[b] + bu * di + bv * dj;
                int best = -1; double bestD = tol;
                foreach (int i in index.Near(target, tol))
                {
                    double dist = (pts[i] - target).Length;
                    if (dist < bestD) { bestD = dist; best = i; }
                }
                if (best < 0 || coords.ContainsKey(best)) continue;
                var c = (coords[b].I + di, coords[b].J + dj);
                if (!taken.Add(c)) continue;
                coords[best] = c;
                var delta = pts[best] - pts[b];
                var nu = dj == 0 ? bu * 0.5 + delta * (0.5 / di) : bu;
                var nv = di == 0 ? bv * 0.5 + delta * (0.5 / dj) : bv;
                local[best] = (nu, nv);
                queue.Enqueue(best);
            }
        }
        return coords;
    }

    internal static double[,] Measure(LuminanceImage img, Homography grid, int columns, int rows)
    {
        const int n = 14;
        var result = new double[columns, rows];
        Span<double> samples = stackalloc double[n * n];
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < columns; i++)
            {
                int k = 0;
                for (int b = 0; b < n; b++)
                    for (int a = 0; a < n; a++)
                    {
                        var p = grid.Map(i - 0.45 + 0.9 * (a + 0.5) / n, j - 0.45 + 0.9 * (b + 0.5) / n);
                        samples[k++] = img.Sample(p.X, p.Y);
                    }
                samples.Sort();
                // The brightest few percent: a lit I and a lit W read the same
                result[i, j] = samples[(int)(samples.Length * 0.96)];
            }
        return result;
    }

    static (string? Text, bool[,] Lit) Decode(double[,] brightness, int columns, int rows)
    {
        var values = brightness.Cast<double>().Order().ToArray();
        double otsu = Otsu(values);
        var thresholds = new List<double> { otsu };
        // Fall back to every split that leaves 30–70 % of the cells lit
        for (int k = values.Length * 3 / 10; k < values.Length * 7 / 10; k++)
            thresholds.Add((values[k] + values[k + 1]) / 2);

        bool[,] firstLit = Threshold(brightness, otsu, columns, rows);
        var decoder = new ZXing.Datamatrix.Internal.Decoder();
        foreach (var t in thresholds)
        {
            var lit = Threshold(brightness, t, columns, rows);
            foreach (var m in Variants(lit, columns, rows))
            {
                if (FinderErrors(m) > 3) continue;
                try
                {
                    var r = decoder.decode(m);
                    if (r?.Text is { Length: > 0 } text) return (text, lit);
                }
                catch (ZXing.ReaderException) { }
                catch (IndexOutOfRangeException) { }
            }
        }
        return (null, firstLit);
    }

    /// <summary>Cells that break the Data Matrix border: solid left and bottom edges, alternating top and right.</summary>
    static int FinderErrors(BitMatrix m)
    {
        int n = m.Width, errors = 0;
        for (int k = 0; k < n; k++)
        {
            if (!m[0, k]) errors++;
            if (!m[k, n - 1]) errors++;
            if (k < n - 1 && m[k, 0] != (k % 2 == 0)) errors++;
            if (k > 0 && k < n - 1 && m[n - 1, k] != (k % 2 == 1)) errors++;
        }
        return errors;
    }

    static bool[,] Threshold(double[,] b, double t, int columns, int rows)
    {
        var lit = new bool[columns, rows];
        for (int j = 0; j < rows; j++) for (int i = 0; i < columns; i++) lit[i, j] = b[i, j] > t;
        return lit;
    }

    static IEnumerable<BitMatrix> Variants(bool[,] lit, int columns, int rows)
    {
        if (columns != rows) yield break;
        int n = columns;
        foreach (bool invert in new[] { false, true })
            foreach (bool mirror in new[] { false, true })
                for (int rot = 0; rot < 4; rot++)
                {
                    var m = new BitMatrix(n, n);
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            int sx = mirror ? n - 1 - x : x, sy = y;
                            for (int r = 0; r < rot; r++) (sx, sy) = (sy, n - 1 - sx);
                            if (lit[sx, sy] != invert) m[x, y] = true;
                        }
                    yield return m;
                }
    }

    static double Otsu(double[] sorted)
    {
        double best = sorted[sorted.Length / 2], bestVar = -1;
        double total = sorted.Sum();
        double left = 0;
        for (int k = 1; k < sorted.Length; k++)
        {
            left += sorted[k - 1];
            double w0 = k, w1 = sorted.Length - k;
            double m0 = left / w0, m1 = (total - left) / w1;
            double between = w0 * w1 * (m0 - m1) * (m0 - m1);
            if (between > bestVar) { bestVar = between; best = (sorted[k - 1] + sorted[k]) / 2; }
        }
        return best;
    }
}

sealed class SpatialIndex
{
    readonly List<Vec2> points;
    readonly double size;
    readonly Dictionary<(int, int), List<int>> buckets = [];

    public SpatialIndex(List<Vec2> points, double size)
    {
        this.points = points;
        this.size = Math.Max(1, size);
        for (int i = 0; i < points.Count; i++)
        {
            var key = ((int)Math.Floor(points[i].X / this.size), (int)Math.Floor(points[i].Y / this.size));
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = [];
            list.Add(i);
        }
    }

    public IEnumerable<int> Near(Vec2 p, double radius)
    {
        int x0 = (int)Math.Floor((p.X - radius) / size), x1 = (int)Math.Floor((p.X + radius) / size);
        int y0 = (int)Math.Floor((p.Y - radius) / size), y1 = (int)Math.Floor((p.Y + radius) / size);
        for (int by = y0; by <= y1; by++)
            for (int bx = x0; bx <= x1; bx++)
                if (buckets.TryGetValue((bx, by), out var list))
                    foreach (int i in list)
                        if ((points[i] - p).Length <= radius) yield return i;
    }
}
