# Plugin.Maui.Spine.Barcodes

Barcodes for .NET: QR, Data Matrix, Aztec, PDF417 and the common linear codes, encoded to a module matrix, an SVG or a `BarcodeView`. A symbol can be held to an exact size, such as a 12 × 12 Data Matrix for a 12 × 12 display. `LightGridReader` reads a code shown by a grid of lamps, such as a word clock, from a camera frame. Built on ZXing.Net; the codec has no MAUI dependency.

```bash
dotnet add package Plugin.Maui.Spine.Barcodes
```

No registration is needed.

```xml
<BarcodeView Value="{Binding Url}" Format="QrCode" HeightRequest="220" />
```

```csharp
var qr = Barcode.Encode(url, new QrCodeOptions { ErrorCorrection = QrErrorCorrection.High });
string svg = qr.ToSvg();

// Exactly 12 × 12, or a BarcodeEncodingException that says why not
var clock = Barcode.Encode("4711081542", new DataMatrixOptions { Size = (12, 12) });
bool lit = clock[x, y];
```

To scan codes with the camera, add [Plugin.Maui.Spine.Scanner](https://www.nuget.org/packages/Plugin.Maui.Spine.Scanner).

Platforms: `net10.0` (encoding and `LightGridReader`, for a server or a test), and Android, iOS, Mac Catalyst and Windows, which add `BarcodeView`.

## Documentation

- [Barcodes and scanning](https://github.com/jonatansoderberg/Maui.Spine/blob/master/docs/wiki/barcodes.md)
- [All packages](https://github.com/jonatansoderberg/Maui.Spine#packages)
