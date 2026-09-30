# Plugin.Maui.Spine.Scanner

Camera barcode scanning for .NET MAUI: a `BarcodeScannerView` and a ready-made scan sheet with breathing aim corners, a burst when a code is read, a sound, a torch and a size chosen per scan. Vision reads the standard codes on iOS and Mac Catalyst, ML Kit on Android. When `LightGrid` is set, a light-grid reader runs next to them and reads a code shown by a grid of lamps, such as a 12 × 12 word clock.

```bash
dotnet add package Plugin.Maui.Spine.Scanner
```

```csharp
builder
    .UseSpine(options => options.AddAssembly(typeof(MauiProgram).Assembly));   // registers the scanner too
    // .UseSpineScanner()                                                     // only without UseSpine
```

```csharp
var scan = await navigation.NavigateToWithResultAsync<BarcodeScannerPage, BarcodeScanOptions, BarcodeScanResult>(
    new BarcodeScanOptions { Formats = BarcodeFormat.QrCode, LightGrid = new LightGridOptions(12, 12) });

if (scan is { IsSuccess: true, Value: { } code })
    await PairAsync(code.Value);
```

```xml
<BarcodeScannerView Formats="QrCode" DetectedCommand="{Binding FoundCommand}" />
```

The app declares the camera on Apple platforms: `NSCameraUsageDescription` in Info.plist. The view says so if it is missing. On Android the package declares `android.permission.CAMERA` itself, and needs API 23 or later.

Platforms: iOS, Mac Catalyst and Android (API 23+). Windows is not supported yet.

## Documentation

- [Barcodes and scanning](https://github.com/jonatansoderberg/Maui.Spine/blob/master/docs/wiki/barcodes.md)
- [All packages](https://github.com/jonatansoderberg/Maui.Spine#packages)
