using System.Globalization;
using SkiaSharp;

namespace Plugin.Maui.Spine.Controls;

/// <summary>
/// One text change in <see cref="AnimatedLabelMode.RollingNumber"/>: the characters that differ roll
/// vertically, like an odometer, while the rest stand still. Has no MAUI types so that it can be
/// compiled into a net10.0 test.
/// </summary>
internal sealed class RollingNumber : IDisposable
{
    internal readonly record struct Column(string? From, string? To, int FromIndex, int ToIndex)
    {
        public bool Rolls => From != To;
    }

    private readonly record struct Glyph(string? From, string? To, float FromX, float ToX)
    {
        public bool Rolls => From != To;
    }

    private readonly Glyph[] _glyphs;
    private readonly SKFont _font;
    private readonly SKPaint _paint;
    private readonly SKColor _color;
    private readonly float _baselinePx;
    private readonly bool _increases;
    private readonly float _fromWidthPx;
    private readonly float _toWidthPx;

    /// <remarks>
    /// Takes ownership of <paramref name="font"/>. <paramref name="fromEnd"/> pairs the characters
    /// from the end, for a text that is aligned to the end.
    /// </remarks>
    public RollingNumber(string from, string to, SKFont font, SKColor color, CultureInfo culture, bool fromEnd = false)
    {
        _font = font;
        _color = color;
        _paint = new SKPaint { IsAntialias = true };
        _increases = Increases(from, to, culture);

        font.GetFontMetrics(out var metrics);
        _baselinePx = -metrics.Ascent;
        // Rounded up like AnimatedLabel's text image, so the last frame lands where the image is drawn.
        HeightPx = MathF.Ceiling(metrics.Descent - metrics.Ascent);

        var fromX = Offsets(from, font, out float fromWidth);
        var toX = Offsets(to, font, out float toWidth);
        var columns = Pair(from, to, fromEnd);
        _fromWidthPx = fromWidth;
        _toWidthPx = toWidth;

        _glyphs = new Glyph[columns.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            var c = columns[i];
            float? startX = c.FromIndex >= 0 ? fromX[c.FromIndex] : null;
            float? endX = c.ToIndex >= 0 ? toX[c.ToIndex] : null;
            _glyphs[i] = new Glyph(c.From, c.To, startX ?? endX!.Value, endX ?? startX!.Value);
        }
    }

    public float HeightPx { get; }

    /// <summary>
    /// Draws the change at <paramref name="progress"/> (0 to 1), with the top of the line at y = 0.
    /// The text is placed in <paramref name="widthPx"/> by <paramref name="align"/>: 0 start, 0.5 centre, 1 end.
    /// </summary>
    public void Draw(SKCanvas canvas, float progress, float widthPx = 0f, float align = 0f)
    {
        float t = Ease(Math.Clamp(progress, 0f, 1f));
        float leave = (_increases ? -HeightPx : HeightPx) * t;
        float arrive = (_increases ? HeightPx : -HeightPx) * (1f - t);
        float fromLeft = Left(widthPx, _fromWidthPx, align);
        float toLeft = Left(widthPx, _toWidthPx, align);

        foreach (var g in _glyphs)
        {
            // A character that arrives or leaves has one place only, and rolls there.
            float startX = g.From is null ? toLeft + g.ToX : fromLeft + g.FromX;
            float endX = g.To is null ? fromLeft + g.FromX : toLeft + g.ToX;
            float x = startX + (endX - startX) * t;

            if (!g.Rolls)
            {
                DrawText(canvas, g.To!, x, _baselinePx, 1f);
                continue;
            }

            if (g.From is not null)
                DrawText(canvas, g.From, x, _baselinePx + leave, 1f - t);

            if (g.To is not null)
                DrawText(canvas, g.To, x, _baselinePx + arrive, t);
        }
    }

    private void DrawText(SKCanvas canvas, string text, float x, float y, float opacity)
    {
        if (opacity <= 0f)
            return;

        _paint.Color = _color.WithAlpha((byte)Math.Round(_color.Alpha * opacity));
        canvas.DrawText(text, x, y, _font, _paint);
    }

    public void Dispose()
    {
        _font.Dispose();
        _paint.Dispose();
    }

    /// <summary>Where a text of <paramref name="textPx"/> starts in <paramref name="availablePx"/>, in whole pixels.</summary>
    internal static float Left(float availablePx, float textPx, float align) =>
        MathF.Max(0f, MathF.Floor((availablePx - textPx) * align));

    /// <summary>The width of <paramref name="text"/> as <see cref="DrawLine"/> draws it.</summary>
    internal static float Measure(string text, SKFont font)
    {
        Offsets(text, font, out float width);
        return width;
    }

    /// <summary>Draws a text at rest the way the last frame of a roll draws it, so the two are the same pixels.</summary>
    internal static void DrawLine(SKCanvas canvas, string text, float baselinePx, SKFont font, SKPaint paint)
    {
        var x = Offsets(text, font, out _);
        var e = StringInfo.GetTextElementEnumerator(text);
        for (int i = 0; e.MoveNext(); i++)
            canvas.DrawText(e.GetTextElement(), x[i], baselinePx, font, paint);
    }

    internal static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    /// <summary>
    /// Pairs the characters of the two texts into columns, from the left: <c>99</c> → <c>100</c> is
    /// 9 → 1, 9 → 0 and a new 0 at the end, so nothing moves sideways. When both texts are built the
    /// same way (<c>9:59</c> and <c>10:00</c>, <c>9 pts</c> and <c>10 pts</c>), each run of digits and
    /// each run of other characters is paired on its own, so the colon and the unit do not roll.
    /// With <paramref name="fromEnd"/> the runs are paired from the right instead, which keeps the
    /// ones over the ones in a text that is aligned to the end.
    /// </summary>
    internal static Column[] Pair(string from, string to, bool fromEnd = false)
    {
        var a = Elements(from);
        var b = Elements(to);
        var runsA = Runs(a);
        var runsB = Runs(b);
        var columns = new List<Column>(Math.Max(a.Length, b.Length));

        bool sameShape = runsA.Count == runsB.Count && runsA.Count > 0 && IsDigit(a[0]) == IsDigit(b[0]);

        if (sameShape)
        {
            for (int i = 0; i < runsA.Count; i++)
                PairRun(a, runsA[i], b, runsB[i], fromEnd, columns);
        }
        else
        {
            PairRun(a, (0, a.Length), b, (0, b.Length), fromEnd, columns);
        }

        return [.. columns];
    }

    private static void PairRun(string[] a, (int Start, int Length) runA, string[] b, (int Start, int Length) runB, bool fromEnd, List<Column> columns)
    {
        int length = Math.Max(runA.Length, runB.Length);
        for (int i = 0; i < length; i++)
        {
            int ka = fromEnd ? runA.Length - length + i : i;
            int kb = fromEnd ? runB.Length - length + i : i;
            int ia = ka >= 0 && ka < runA.Length ? runA.Start + ka : -1;
            int ib = kb >= 0 && kb < runB.Length ? runB.Start + kb : -1;
            columns.Add(new Column(ia >= 0 ? a[ia] : null, ib >= 0 ? b[ib] : null, ia, ib));
        }
    }

    // Consecutive digits, and consecutive characters that are not digits.
    private static List<(int Start, int Length)> Runs(string[] elements)
    {
        var runs = new List<(int Start, int Length)>();
        int start = 0;
        for (int i = 1; i <= elements.Length; i++)
        {
            if (i < elements.Length && IsDigit(elements[i]) == IsDigit(elements[start]))
                continue;

            runs.Add((start, i - start));
            start = i;
        }

        return runs;
    }

    /// <summary>
    /// Whether the number went up, which decides the direction of the roll. The number is read with
    /// <paramref name="culture"/>, then invariantly; a text that is neither (<c>12:59</c>) compares its
    /// digits in order. Without digits, or with equal ones, it counts as up.
    /// </summary>
    internal static bool Increases(string from, string to, CultureInfo culture)
    {
        var a = NumberPart(from);
        var b = NumberPart(to);

        if (TryParse(a, culture, out var x) && TryParse(b, culture, out var y))
            return y >= x;

        int sign = DigitSign(a, out var da);
        int otherSign = DigitSign(b, out var db);

        if (sign != otherSign)
            return otherSign > sign;

        int order = CompareDigits(da, db);
        return order == 0 || (sign < 0 ? order > 0 : order < 0);
    }

    private static bool TryParse(string text, CultureInfo culture, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, culture, out value) ||
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    // From a sign or first digit to the last digit: "Score -12 pts" → "-12".
    private static string NumberPart(string text)
    {
        int first = -1;
        int last = -1;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsAsciiDigit(text[i]))
                continue;
            if (first < 0)
                first = i;
            last = i;
        }

        if (first < 0)
            return string.Empty;

        if (first > 0 && IsMinus(text[first - 1]))
            first--;

        return text[first..(last + 1)].Replace('−', '-');
    }

    private static int DigitSign(string number, out string digits)
    {
        digits = string.Concat(number.Where(char.IsAsciiDigit)).TrimStart('0');
        return number.Length > 0 && number[0] == '-' && digits.Length > 0 ? -1 : 1;
    }

    private static int CompareDigits(string a, string b) =>
        a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);

    private static bool IsMinus(char c) => c is '-' or '−';

    private static bool IsDigit(string element) => element.Length == 1 && char.IsAsciiDigit(element[0]);

    private static string[] Elements(string text)
    {
        var elements = new List<string>(text.Length);
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
            elements.Add(e.GetTextElement());
        return [.. elements];
    }

    // Every digit gets a cell as wide as the font's widest digit and is centred in it (tabular
    // figures), so a number keeps its width while it counts. Other characters are measured over
    // their run rather than summed one by one, which keeps their kerning.
    private static float[] Offsets(string text, SKFont font, out float width)
    {
        var elements = Elements(text);
        var offsets = new float[elements.Length];
        float cell = DigitCell(font);
        float cursor = 0f;

        foreach (var (start, length) in Runs(elements))
        {
            if (IsDigit(elements[start]))
            {
                for (int i = start; i < start + length; i++)
                {
                    offsets[i] = cursor + (cell - font.MeasureText(elements[i])) * 0.5f;
                    cursor += cell;
                }

                continue;
            }

            string run = string.Empty;
            for (int i = start; i < start + length; i++)
            {
                offsets[i] = cursor + (run.Length == 0 ? 0f : font.MeasureText(run));
                run += elements[i];
            }

            cursor += font.MeasureText(run);
        }

        width = cursor;
        return offsets;
    }

    private static float DigitCell(SKFont font)
    {
        float cell = 0f;
        for (char d = '0'; d <= '9'; d++)
            cell = MathF.Max(cell, font.MeasureText(d.ToString()));
        return cell;
    }
}
