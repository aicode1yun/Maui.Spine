namespace Plugin.Maui.Spine.Barcodes;

/// <summary>
/// The barcode symbologies Spine can draw and read. Encoding takes one format; a scanner takes a combination.
/// </summary>
[Flags]
public enum BarcodeFormat
{
    None = 0,

    /// <summary>QR Code (ISO/IEC 18004), 21 × 21 modules and up.</summary>
    QrCode = 1 << 0,

    /// <summary>Data Matrix ECC 200 (ISO/IEC 16022), 10 × 10 modules and up; 12 × 12 holds ten digits.</summary>
    DataMatrix = 1 << 1,

    /// <summary>Aztec (ISO/IEC 24778), 15 × 15 modules and up, no quiet zone needed.</summary>
    Aztec = 1 << 2,

    /// <summary>PDF417 (ISO/IEC 15438), stacked rows.</summary>
    Pdf417 = 1 << 3,

    /// <summary>Code 128, full ASCII.</summary>
    Code128 = 1 << 4,

    /// <summary>Code 39: digits, upper-case letters and a few symbols.</summary>
    Code39 = 1 << 5,

    /// <summary>Code 93, a denser Code 39.</summary>
    Code93 = 1 << 6,

    /// <summary>EAN-13: 12 digits and a check digit.</summary>
    Ean13 = 1 << 7,

    /// <summary>EAN-8: 7 digits and a check digit.</summary>
    Ean8 = 1 << 8,

    /// <summary>UPC-A: 11 digits and a check digit.</summary>
    UpcA = 1 << 9,

    /// <summary>UPC-E: the zero-suppressed 6-digit UPC.</summary>
    UpcE = 1 << 10,

    /// <summary>Interleaved 2 of 5: an even number of digits.</summary>
    Itf = 1 << 11,

    /// <summary>Codabar: digits and <c>-$:/.+</c>, framed by A–D.</summary>
    Codabar = 1 << 12,

    /// <summary>The matrix and stacked codes.</summary>
    TwoDimensional = QrCode | DataMatrix | Aztec | Pdf417,

    /// <summary>The linear codes.</summary>
    OneDimensional = Code128 | Code39 | Code93 | Ean13 | Ean8 | UpcA | UpcE | Itf | Codabar,

    All = TwoDimensional | OneDimensional,
}
