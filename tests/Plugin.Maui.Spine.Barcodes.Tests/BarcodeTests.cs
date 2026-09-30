using ZXing;
using ZXing.Common;
using Xunit;

namespace Plugin.Maui.Spine.Barcodes.Tests;

/// <summary>Encoding every format, fixed Data Matrix sizes and SVG output.</summary>
public class BarcodeTests
{
    public static TheoryData<BarcodeFormat, string> Samples => new()
    {
        { BarcodeFormat.QrCode, "https://github.com/jonatansoderberg/Maui.Spine?å=ö" },
        { BarcodeFormat.DataMatrix, "4711081542" },
        { BarcodeFormat.Aztec, "Spine Aztec 2026" },
        { BarcodeFormat.Pdf417, "Spine PDF417" },
        { BarcodeFormat.Code128, "SPINE-128" },
        { BarcodeFormat.Code39, "SPINE39" },
        { BarcodeFormat.Code93, "SPINE93" },
        { BarcodeFormat.Ean13, "590123412345" },
        { BarcodeFormat.Ean8, "9638507" },
        { BarcodeFormat.UpcA, "03600029145" },
        { BarcodeFormat.UpcE, "0123456" },
        { BarcodeFormat.Itf, "12345678" },
        { BarcodeFormat.Codabar, "A40156B" },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_format_reads_back(BarcodeFormat format, string value)
    {
        var matrix = Barcode.Encode(value, format);

        var decoded = Decode(matrix, format);

        // EAN and UPC add their check digit
        Assert.NotNull(decoded);
        Assert.StartsWith(value, decoded);
    }

    [Fact]
    public void A_fixed_12x12_data_matrix_holds_ten_digits()
    {
        var matrix = Barcode.Encode("4711081542", new DataMatrixOptions { Size = (12, 12) });

        Assert.Equal(12, matrix.Width);
        Assert.Equal(12, matrix.Height);
        Assert.Equal("4711081542", Decode(matrix, BarcodeFormat.DataMatrix));
    }

    [Fact]
    public void A_payload_too_long_for_the_fixed_size_says_so()
    {
        var ex = Assert.Throws<BarcodeEncodingException>(() => Barcode.Encode("47110815421", new DataMatrixOptions { Size = (12, 12) }));

        Assert.Contains("12 × 12", ex.Message);
        Assert.Contains("11 characters", ex.Message);
    }

    [Fact]
    public void Characters_a_format_cannot_carry_fail_with_the_format_named()
    {
        var ex = Assert.Throws<BarcodeEncodingException>(() => Barcode.Encode("ABC", BarcodeFormat.Ean13));

        Assert.Contains("Ean13", ex.Message);
    }

    [Fact]
    public void Encoding_takes_one_format()
    {
        Assert.Throws<ArgumentException>(() => Barcode.Encode("x", new BarcodeOptions(BarcodeFormat.QrCode | BarcodeFormat.Aztec)));
    }

    [Theory]
    [InlineData(QrErrorCorrection.Low)]
    [InlineData(QrErrorCorrection.High)]
    public void Qr_error_correction_changes_the_symbol(QrErrorCorrection level)
    {
        var low = Barcode.Encode(new string('x', 60), new QrCodeOptions { ErrorCorrection = QrErrorCorrection.Low });
        var chosen = Barcode.Encode(new string('x', 60), new QrCodeOptions { ErrorCorrection = level });

        Assert.True(chosen.Width >= low.Width);
        Assert.Equal(new string('x', 60), Decode(chosen, BarcodeFormat.QrCode));
    }

    [Fact]
    public void The_quiet_zone_defaults_to_the_standard_and_can_be_set()
    {
        Assert.Equal(4, Barcode.Encode("a", BarcodeFormat.QrCode).QuietZone);
        Assert.Equal(1, Barcode.Encode("1", BarcodeFormat.DataMatrix).QuietZone);
        Assert.Equal(0, Barcode.Encode("a", new QrCodeOptions { QuietZone = 0 }).QuietZone);
    }

    [Fact]
    public void Linear_codes_are_one_row()
    {
        Assert.True(Barcode.Encode("SPINE", BarcodeFormat.Code128).IsLinear);
        Assert.False(Barcode.Encode("SPINE", BarcodeFormat.QrCode).IsLinear);
    }

    [Fact]
    public void Svg_has_the_quiet_zone_in_its_view_box()
    {
        var matrix = Barcode.Encode("4711081542", new DataMatrixOptions { Size = (12, 12) });

        var svg = matrix.ToSvg();

        Assert.StartsWith("<svg", svg);
        Assert.Contains("viewBox=\"0 0 14 14\"", svg);
        Assert.Contains("<path", svg);
    }

    /// <summary>Renders the matrix at 4 px per module, quiet zone included, and reads it with ZXing.</summary>
    internal static string? Decode(BarcodeMatrix matrix, BarcodeFormat format)
    {
        const int scale = 4;
        int q = Math.Max(matrix.QuietZone, 2);
        int rows = matrix.IsLinear ? 30 : matrix.Height;
        int w = (matrix.Width + 2 * q) * scale, h = (rows + 2 * q) * scale;
        var pixels = new byte[w * h];
        Array.Fill(pixels, (byte)255);
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < matrix.Width; x++)
                if (matrix[x, matrix.IsLinear ? 0 : y])
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                            pixels[((y + q) * scale + dy) * w + (x + q) * scale + dx] = 0;

        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [Barcode.ToZXing(format)], TryHarder = true, ReturnCodabarStartEnd = true },
        };
        return reader.Decode(new RGBLuminanceSource(pixels, w, h, RGBLuminanceSource.BitmapFormat.Gray8))?.Text;
    }
}
