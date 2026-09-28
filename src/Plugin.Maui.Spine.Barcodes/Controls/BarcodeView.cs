namespace Plugin.Maui.Spine.Barcodes;

/// <summary>How <see cref="BarcodeView"/> draws each dark module of a 2D code.</summary>
public enum ModuleShape
{
    Square,

    /// <summary>A round dot per module; decorative, and still read by phone cameras at a normal size.</summary>
    Dot,
}

/// <summary>
/// Draws a barcode: set <see cref="Value"/> and <see cref="Format"/>, or hand it a ready <see cref="Matrix"/>.
/// A 2D code keeps its aspect and centres itself; a linear code fills the width and height it is given.
/// </summary>
/// <remarks>
/// The quiet zone is drawn in <see cref="VisualElement.BackgroundColor"/>, white unless set, because a scanner
/// needs light around the code. When the value cannot be encoded nothing is drawn and <see cref="Error"/> says why.
/// </remarks>
/// <example>
/// <code language="xml"><![CDATA[
/// <BarcodeView Value="{Binding Url}" Format="QrCode" WidthRequest="200" HeightRequest="200" />
/// ]]></code>
/// </example>
public class BarcodeView : GraphicsView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(string), typeof(BarcodeView), propertyChanged: (b, _, _) => ((BarcodeView)b).Rebuild());

    public static readonly BindableProperty FormatProperty = BindableProperty.Create(
        nameof(Format), typeof(BarcodeFormat), typeof(BarcodeView), BarcodeFormat.QrCode, propertyChanged: (b, _, _) => ((BarcodeView)b).Rebuild());

    public static readonly BindableProperty OptionsProperty = BindableProperty.Create(
        nameof(Options), typeof(BarcodeOptions), typeof(BarcodeView), propertyChanged: (b, _, _) => ((BarcodeView)b).Rebuild());

    public static readonly BindableProperty MatrixProperty = BindableProperty.Create(
        nameof(Matrix), typeof(BarcodeMatrix), typeof(BarcodeView), propertyChanged: (b, _, _) => ((BarcodeView)b).OnMatrixChanged());

    public static readonly BindableProperty ModuleColorProperty = BindableProperty.Create(
        nameof(ModuleColor), typeof(Color), typeof(BarcodeView), Colors.Black, propertyChanged: (b, _, _) => ((BarcodeView)b).Invalidate());

    public static readonly BindableProperty IsInvertedProperty = BindableProperty.Create(
        nameof(IsInverted), typeof(bool), typeof(BarcodeView), false, propertyChanged: (b, _, _) => ((BarcodeView)b).Invalidate());

    public static readonly BindableProperty ModuleShapeProperty = BindableProperty.Create(
        nameof(ModuleShape), typeof(ModuleShape), typeof(BarcodeView), ModuleShape.Square, propertyChanged: (b, _, _) => ((BarcodeView)b).Invalidate());

    private static readonly BindablePropertyKey ErrorPropertyKey = BindableProperty.CreateReadOnly(
        nameof(Error), typeof(string), typeof(BarcodeView), null);

    public static readonly BindableProperty ErrorProperty = ErrorPropertyKey.BindableProperty;

    private readonly BarcodeDrawable _drawable = new();
    private bool _settingMatrix;

    public BarcodeView()
    {
        BackgroundColor = Colors.White;
        Drawable = _drawable;
        _drawable.View = this;
    }

    /// <summary>The text to encode.</summary>
    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The symbology, used with its default options unless <see cref="Options"/> is set.</summary>
    public BarcodeFormat Format
    {
        get => (BarcodeFormat)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Options for the code, such as a <see cref="QrCodeOptions"/>; when set, its format wins over <see cref="Format"/>.</summary>
    public BarcodeOptions? Options
    {
        get => (BarcodeOptions?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>The modules being drawn. Set it to draw a matrix built elsewhere; it is replaced when <see cref="Value"/> changes.</summary>
    public BarcodeMatrix? Matrix
    {
        get => (BarcodeMatrix?)GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    /// <summary>The colour of the dark modules (the bars).</summary>
    public Color ModuleColor
    {
        get => (Color)GetValue(ModuleColorProperty);
        set => SetValue(ModuleColorProperty, value);
    }

    /// <summary>Swaps the two colours, for a light code on a dark display. Most phone cameras read it; not every scanner does.</summary>
    public bool IsInverted
    {
        get => (bool)GetValue(IsInvertedProperty);
        set => SetValue(IsInvertedProperty, value);
    }

    public ModuleShape ModuleShape
    {
        get => (ModuleShape)GetValue(ModuleShapeProperty);
        set => SetValue(ModuleShapeProperty, value);
    }

    /// <summary>Why <see cref="Value"/> could not be encoded, or <see langword="null"/>.</summary>
    public string? Error => (string?)GetValue(ErrorProperty);

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        // Without a size of its own, a 2D code takes the height its modules need at the width it is offered
        bool sized = WidthRequest >= 0 || HeightRequest >= 0;
        if (!sized && Matrix is { IsLinear: false } m && (double.IsInfinity(widthConstraint) || double.IsInfinity(heightConstraint)))
        {
            double aspect = (double)(m.Height + 2 * m.QuietZone) / (m.Width + 2 * m.QuietZone);
            if (!double.IsInfinity(widthConstraint)) return new Size(widthConstraint, widthConstraint * aspect);
            if (!double.IsInfinity(heightConstraint)) return new Size(heightConstraint / aspect, heightConstraint);
        }
        return base.MeasureOverride(widthConstraint, heightConstraint);
    }

    private void Rebuild()
    {
        string? error = null;
        BarcodeMatrix? matrix = null;
        if (!string.IsNullOrEmpty(Value))
        {
            try { matrix = Barcode.Encode(Value, Options ?? Barcode.DefaultOptions(Format)); }
            catch (Exception ex) when (ex is BarcodeEncodingException or ArgumentException) { error = ex.Message; }
        }
        SetValue(ErrorPropertyKey, error);
        _settingMatrix = true;
        try { Matrix = matrix; }
        finally { _settingMatrix = false; }
        if (matrix is null) OnMatrixChanged();
    }

    private void OnMatrixChanged()
    {
        if (!_settingMatrix) SetValue(ErrorPropertyKey, null);
        InvalidateMeasure();
        Invalidate();
    }

    private sealed class BarcodeDrawable : IDrawable
    {
        public BarcodeView? View;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (View?.Matrix is not { } m) return;
            var light = View.BackgroundColor ?? Colors.White;
            var dark = View.ModuleColor;
            if (View.IsInverted) (light, dark) = (dark, light);

            canvas.FillColor = light;
            canvas.FillRectangle(dirtyRect);
            canvas.FillColor = dark;

            // A module that is a whole number of screen pixels, on the pixel grid: fractional modules leave hairline
            // seams between rows, and a scanner reads uneven modules less well
            float density = (float)(View.Window?.DisplayDensity ?? 0);
            if (density <= 0) density = (float)DeviceDisplay.Current.MainDisplayInfo.Density;
            if (density <= 0) density = 1;
            float Snap(float v) => MathF.Round(v * density) / density;
            float Whole(float v) => Math.Max(1, MathF.Floor(v * density)) / density;

            int q = m.QuietZone;
            if (m.IsLinear)
            {
                float unit = Whole(dirtyRect.Width / (m.Width + 2 * q));
                float start0 = Snap(dirtyRect.Center.X - unit * m.Width / 2);
                canvas.Antialias = false;
                for (int x = 0; x < m.Width; x++)
                {
                    if (!m[x, 0]) continue;
                    int start = x;
                    while (x + 1 < m.Width && m[x + 1, 0]) x++;
                    canvas.FillRectangle(start0 + start * unit, dirtyRect.Y, (x - start + 1) * unit, dirtyRect.Height);
                }
                return;
            }

            float module = Whole(Math.Min(dirtyRect.Width / (m.Width + 2 * q), dirtyRect.Height / (m.Height + 2 * q)));
            float left = Snap(dirtyRect.Center.X - module * m.Width / 2), top = Snap(dirtyRect.Center.Y - module * m.Height / 2);
            if (View.ModuleShape == ModuleShape.Dot)
            {
                canvas.Antialias = true;
                float r = module * 0.45f;
                for (int y = 0; y < m.Height; y++)
                    for (int x = 0; x < m.Width; x++)
                        if (m[x, y]) canvas.FillCircle(left + (x + 0.5f) * module, top + (y + 0.5f) * module, r);
                return;
            }

            // Horizontal runs, and no anti-aliasing, so neighbouring modules do not show seams
            canvas.Antialias = false;
            for (int y = 0; y < m.Height; y++)
                for (int x = 0; x < m.Width; x++)
                {
                    if (!m[x, y]) continue;
                    int start = x;
                    while (x + 1 < m.Width && m[x + 1, y]) x++;
                    canvas.FillRectangle(left + start * module, top + y * module, (x - start + 1) * module, module);
                }
        }
    }
}
