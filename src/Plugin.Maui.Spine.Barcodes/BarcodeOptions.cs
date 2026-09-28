namespace Plugin.Maui.Spine.Barcodes;

/// <summary>
/// How <see cref="Barcode.Encode(string, BarcodeOptions)"/> builds a code. The base type covers the linear
/// codes; the 2D formats have their own options with the settings their standard defines.
/// </summary>
public record BarcodeOptions
{
    public BarcodeOptions() { }

    public BarcodeOptions(BarcodeFormat format) => Format = format;

    /// <summary>The symbology; exactly one flag.</summary>
    public BarcodeFormat Format { get; init; } = BarcodeFormat.QrCode;

    /// <summary>
    /// Light modules to keep around the code when it is drawn, or <see langword="null"/> for the
    /// standard's minimum (4 for QR, 1 for Data Matrix, 0 for Aztec, 2 for PDF417, 10 for linear codes).
    /// The matrix itself never includes it.
    /// </summary>
    public int? QuietZone { get; init; }

    /// <summary>
    /// The character set for text outside ASCII, such as <c>UTF-8</c>, or <see langword="null"/> for the
    /// format's default (ISO-8859-1 for QR).
    /// </summary>
    public string? CharacterSet { get; init; }
}

/// <summary>QR error correction: the share of the code that may be damaged and still read.</summary>
public enum QrErrorCorrection
{
    /// <summary>About 7 %.</summary>
    Low,

    /// <summary>About 15 %.</summary>
    Medium,

    /// <summary>About 25 %.</summary>
    Quartile,

    /// <summary>About 30 %.</summary>
    High,
}

/// <summary>Options for <see cref="BarcodeFormat.QrCode"/>.</summary>
public sealed record QrCodeOptions : BarcodeOptions
{
    public QrCodeOptions() : base(BarcodeFormat.QrCode) { }

    public QrErrorCorrection ErrorCorrection { get; init; } = QrErrorCorrection.Medium;

    /// <summary>A fixed version from 1 (21 × 21) to 40 (177 × 177), or <see langword="null"/> for the smallest that fits.</summary>
    public int? Version { get; init; }
}

/// <summary>The outline of a Data Matrix symbol.</summary>
public enum DataMatrixShape
{
    /// <summary>Whichever fits the payload in the fewest modules.</summary>
    Any,
    Square,
    Rectangle,
}

/// <summary>Options for <see cref="BarcodeFormat.DataMatrix"/>.</summary>
public sealed record DataMatrixOptions : BarcodeOptions
{
    public DataMatrixOptions() : base(BarcodeFormat.DataMatrix) { }

    public DataMatrixShape Shape { get; init; } = DataMatrixShape.Square;

    /// <summary>
    /// The exact symbol size in modules, such as 12 × 12 for a 12 × 12 display; encoding fails when the payload
    /// does not fit. <see langword="null"/> picks the smallest symbol that holds the payload.
    /// </summary>
    public (int Columns, int Rows)? Size { get; init; }
}

/// <summary>Options for <see cref="BarcodeFormat.Aztec"/>.</summary>
public sealed record AztecOptions : BarcodeOptions
{
    public AztecOptions() : base(BarcodeFormat.Aztec) { }

    /// <summary>The share of the symbol spent on error correction, in percent; the standard recommends 23 or more.</summary>
    public int ErrorCorrectionPercent { get; init; } = 33;

    /// <summary>A fixed layer count (negative for the compact form), or <see langword="null"/> for the smallest that fits.</summary>
    public int? Layers { get; init; }
}

/// <summary>Options for <see cref="BarcodeFormat.Pdf417"/>.</summary>
public sealed record Pdf417Options : BarcodeOptions
{
    public Pdf417Options() : base(BarcodeFormat.Pdf417) { }

    /// <summary>Error correction level 0–8; each level doubles the correction codewords.</summary>
    public int ErrorCorrectionLevel { get; init; } = 2;

    /// <summary>The compact form, without the right-hand row indicators.</summary>
    public bool Compact { get; init; }
}
