using ZXing;
using ZXing.Aztec;
using ZXing.Common;
using ZXing.Datamatrix.Encoder;
using ZXing.PDF417.Internal;
using ZXing.QrCode.Internal;

namespace Plugin.Maui.Spine.Barcodes;

/// <summary>Builds barcodes as a <see cref="BarcodeMatrix"/>.</summary>
/// <example>
/// <code><![CDATA[
/// var qr = Barcode.Encode("https://example.com", BarcodeFormat.QrCode);
/// var clock = Barcode.Encode("4711081542", new DataMatrixOptions { Size = (12, 12) });
/// ]]></code>
/// </example>
public static class Barcode
{
    /// <summary>Encodes <paramref name="value"/> with the format's default options.</summary>
    public static BarcodeMatrix Encode(string value, BarcodeFormat format) => Encode(value, DefaultOptions(format));

    /// <summary>Encodes <paramref name="value"/>.</summary>
    /// <exception cref="BarcodeEncodingException">The value is not valid for the format, or does not fit the requested size.</exception>
    public static BarcodeMatrix Encode(string value, BarcodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        var format = options.Format;
        if (format == BarcodeFormat.None || (format & (format - 1)) != 0)
            throw new ArgumentException($"Encoding takes exactly one format, not {format}.", nameof(options));

        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 };
        if (options.CharacterSet is { } charset) hints[EncodeHintType.CHARACTER_SET] = charset;
        AddFormatHints(options, hints);

        BitMatrix bits;
        try
        {
            // Width and height 0 ask ZXing for one pixel per module
            bits = new MultiFormatWriter().encode(value, ToZXing(format), 0, 0, hints);
        }
        catch (Exception ex) when (ex is ArgumentException or WriterException or InvalidOperationException)
        {
            throw new BarcodeEncodingException(Describe(value, options) + ": " + ex.Message, ex);
        }

        // Linear writers return a single row; 2D writers return the symbol itself
        var matrix = new BarcodeMatrix(bits.Width, bits.Height, options.QuietZone ?? DefaultQuietZone(format));
        for (int y = 0; y < bits.Height; y++)
            for (int x = 0; x < bits.Width; x++)
                if (bits[x, y]) matrix[x, y] = true;

        if (options is DataMatrixOptions { Size: { } size } && (matrix.Width != size.Columns || matrix.Height != size.Rows))
            throw new BarcodeEncodingException($"{Describe(value, options)}: got a {matrix.Width} × {matrix.Height} symbol, not {size.Columns} × {size.Rows}.");
        return matrix;
    }

    /// <summary>The options <see cref="Encode(string, BarcodeFormat)"/> uses for <paramref name="format"/>.</summary>
    public static BarcodeOptions DefaultOptions(BarcodeFormat format) => format switch
    {
        BarcodeFormat.QrCode => new QrCodeOptions(),
        BarcodeFormat.DataMatrix => new DataMatrixOptions(),
        BarcodeFormat.Aztec => new AztecOptions(),
        BarcodeFormat.Pdf417 => new Pdf417Options(),
        _ => new BarcodeOptions(format),
    };

    /// <summary>The standard's minimum quiet zone, in modules.</summary>
    public static int DefaultQuietZone(BarcodeFormat format) => format switch
    {
        BarcodeFormat.QrCode => 4,
        BarcodeFormat.DataMatrix => 1,
        BarcodeFormat.Aztec => 0,
        BarcodeFormat.Pdf417 => 2,
        _ => 10,
    };

    private static void AddFormatHints(BarcodeOptions options, Dictionary<EncodeHintType, object> hints)
    {
        switch (options)
        {
            case QrCodeOptions qr:
                hints[EncodeHintType.ERROR_CORRECTION] = qr.ErrorCorrection switch
                {
                    QrErrorCorrection.Low => ErrorCorrectionLevel.L,
                    QrErrorCorrection.Quartile => ErrorCorrectionLevel.Q,
                    QrErrorCorrection.High => ErrorCorrectionLevel.H,
                    _ => ErrorCorrectionLevel.M,
                };
                if (qr.Version is { } version) hints[EncodeHintType.QR_VERSION] = version;
                break;
            case DataMatrixOptions dm:
                hints[EncodeHintType.DATA_MATRIX_SHAPE] = dm.Shape switch
                {
                    DataMatrixShape.Square => SymbolShapeHint.FORCE_SQUARE,
                    DataMatrixShape.Rectangle => SymbolShapeHint.FORCE_RECTANGLE,
                    _ => SymbolShapeHint.FORCE_NONE,
                };
                if (dm.Size is { } size)
                {
                    hints[EncodeHintType.MIN_SIZE] = new Dimension(size.Columns, size.Rows);
                    hints[EncodeHintType.MAX_SIZE] = new Dimension(size.Columns, size.Rows);
                }
                break;
            case AztecOptions aztec:
                hints[EncodeHintType.ERROR_CORRECTION] = aztec.ErrorCorrectionPercent;
                if (aztec.Layers is { } layers) hints[EncodeHintType.AZTEC_LAYERS] = layers;
                break;
            case Pdf417Options pdf:
                hints[EncodeHintType.ERROR_CORRECTION] = (PDF417ErrorCorrectionLevel)Math.Clamp(pdf.ErrorCorrectionLevel, 0, 8);
                hints[EncodeHintType.PDF417_COMPACT] = pdf.Compact;
                break;
        }
    }

    private static string Describe(string value, BarcodeOptions options)
    {
        string size = options is DataMatrixOptions { Size: { } s } ? $" {s.Columns} × {s.Rows}" : "";
        return $"Cannot encode {value.Length} characters as {options.Format}{size}";
    }

    internal static ZXing.BarcodeFormat ToZXing(BarcodeFormat format) => format switch
    {
        BarcodeFormat.QrCode => ZXing.BarcodeFormat.QR_CODE,
        BarcodeFormat.DataMatrix => ZXing.BarcodeFormat.DATA_MATRIX,
        BarcodeFormat.Aztec => ZXing.BarcodeFormat.AZTEC,
        BarcodeFormat.Pdf417 => ZXing.BarcodeFormat.PDF_417,
        BarcodeFormat.Code128 => ZXing.BarcodeFormat.CODE_128,
        BarcodeFormat.Code39 => ZXing.BarcodeFormat.CODE_39,
        BarcodeFormat.Code93 => ZXing.BarcodeFormat.CODE_93,
        BarcodeFormat.Ean13 => ZXing.BarcodeFormat.EAN_13,
        BarcodeFormat.Ean8 => ZXing.BarcodeFormat.EAN_8,
        BarcodeFormat.UpcA => ZXing.BarcodeFormat.UPC_A,
        BarcodeFormat.UpcE => ZXing.BarcodeFormat.UPC_E,
        BarcodeFormat.Itf => ZXing.BarcodeFormat.ITF,
        BarcodeFormat.Codabar => ZXing.BarcodeFormat.CODABAR,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };
}

/// <summary>A value could not be encoded: wrong characters for the format, or too long for the requested size.</summary>
public sealed class BarcodeEncodingException(string message, Exception? inner = null) : Exception(message, inner);
