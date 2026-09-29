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

    /// <remarks>Takes ownership of <paramref name="font"/>.</remarks>
    public RollingNumber(string from, string to, SKFont font, SKColor color, CultureInfo culture)
    {
        _font = font;
        _color = color;
        _paint = new SKPaint { IsAntialias = true };
        _increases = Increases(from, to, culture);

        font.GetFontMetrics(out var metrics);
        _baselinePx = -metrics.Ascent;
        // Rounded up like AnimatedLabel's text image, so the last frame lands where the image is drawn.
        HeightPx = MathF.Ceiling(metrics.Descent - metrics.Ascent);

        var fromX = Offsets(from, font);
        var toX = Offsets(to, font);
        var columns = Pair(from, to);

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

    /// <summary>Draws the change at <paramref name="progress"/> (0 to 1), with the top of the line at y = 0.</summary>
    public void Draw(SKCanvas canvas, float progress)
    {
        float t = Ease(Math.Clamp(progress, 0f, 1f));
        float leave = (_increases ? -HeightPx : HeightPx) * t;
        float arrive = (_increases ? HeightPx : -HeightPx) * (1f - t);

        foreach (var g in _glyphs)
        {
            float x = g.FromX + (g.ToX - g.FromX) * t;

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

    internal static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    /// <summary>
    /// Pairs the characters of the two texts into columns. A shared prefix without digits stays put
    /// (<c>Score 9</c> → <c>Score 10</c>); the rest is aligned from the right, so the ones stay over
    /// the ones when the number gains or loses a digit.
    /// </summary>
    internal static Column[] Pair(string from, string to)
    {
        var a = Elements(from);
        var b = Elements(to);

        int prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix] && !IsDigit(a[prefix]))
            prefix++;

        int tail = Math.Max(a.Length, b.Length) - prefix;
        var columns = new Column[prefix + tail];

        for (int i = 0; i < prefix; i++)
            columns[i] = new Column(a[i], b[i], i, i);

        for (int i = 0; i < tail; i++)
        {
            int ia = a.Length - tail + i;
            int ib = b.Length - tail + i;
            if (ia < prefix) ia = -1;
            if (ib < prefix) ib = -1;
            columns[prefix + i] = new Column(ia >= 0 ? a[ia] : null, ib >= 0 ? b[ib] : null, ia, ib);
        }

        return columns;
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

    // Measured over the whole prefix rather than summed per element, so kerning matches the final text.
    private static float[] Offsets(string text, SKFont font)
    {
        var offsets = new List<float>(text.Length);
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
            offsets.Add(e.ElementIndex == 0 ? 0f : font.MeasureText(text.AsSpan(0, e.ElementIndex)));
        return [.. offsets];
    }
}
