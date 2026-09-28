# Issue #415 — Barcodes: generate QR, Data Matrix and custom codes, and scan them with the camera

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/415
**Branch:** issue/415-barcodes-generate-qr-data-matrix-and-custom-codes
**Status:** In Progress

## Plan

The spike (issue comment 2026-09-28) proved the word-clock case on a real iPhone: Data Matrix 12 × 12, read live from a grid of letters at 9–12 ms per frame. This issue turns it into two packages and a Showcase page. Android scanning is in scope. `GridCode` (a custom symbology for other grid sizes) comes later.

### `Plugin.Maui.Spine.Barcodes`
No camera and no permissions. Targets `net10.0` as well as the MAUI TFMs, so the codec runs on a server and in the tests.
- **`Barcode.Encode(string, BarcodeOptions)`** returns a `BarcodeMatrix` (width, height, `this[x, y]`). The options:
  - common: `BarcodeFormat`, `QuietZone`
  - per format: `QrCodeOptions` (error correction level, version), `DataMatrixOptions` (square/rectangle, fixed size such as 12 × 12), `AztecOptions`, `Pdf417Options`
  - 1D formats take no extra options
- **Formats:** QR, Data Matrix, Aztec, PDF417, Code 128, Code 39, Code 93, EAN-13, EAN-8, UPC-A, UPC-E, ITF and Codabar, through ZXing.Net behind Spine's own types.
- **`BarcodeMatrix.ToSvg(...)`** for export.
- **`BarcodeView`** (MAUI TFMs only), drawn with a `GraphicsView` and `IDrawable` like Shimmer:
  - `Value`, `Format`, `Options`
  - `Foreground`, `Background`, `IsInverted`, `ModuleShape` (square or dot)
  - `Matrix`, to draw a matrix the app built itself
- **`LightGridReader`**, the spike's reader: it reads a matrix code shown on a grid of light sources and returns `LightGridResult` (text, the grid's corners in the image, the log). It runs on a luminance buffer (`ReadOnlySpan<byte>`, width, height, stride) with a reusable `LightGridReader` instance that owns its scratch memory.

### `Plugin.Maui.Spine.Scanner`
Camera scanning. References Barcodes and Plugin.Maui.Spine.
- **`BarcodeScannerView`**:
  - `Formats` (flags) and `LightGrid` (e.g. `new LightGridOptions(12, 12)`; off by default)
  - `IsScanning`, `IsTorchOn`
  - a `Detected` event and `DetectedCommand` with `BarcodeScanResult` (value, format, corners)
  - a `Problem` event: permission denied, no camera, session interrupted, no frames
- **iOS / Mac Catalyst:** `AVCaptureSession` 1280 × 720 in bi-planar full-range format. On the frame queue, `VNDetectBarcodesRequest` handles the standard formats and `LightGridReader` reads the Y plane. Also:
  - a watchdog that restarts the session after 2 s without frames;
  - runtime-error and interruption notifications;
  - explicit teardown on disconnect.
- **Android:** CameraX `PreviewView` + `ImageAnalysis` (`STRATEGY_KEEP_ONLY_LATEST`, YUV_420_888). ML Kit barcode scanning, with the bundled model, handles the standard formats, and `LightGridReader` reads the Y plane. The camera is bound to the activity lifecycle.
- **`BarcodeScannerPage`:** a ready-made `[NavigableSheet]` with a torch page action and cancel. It implements `INavigableWithParameter<BarcodeScanOptions>` and `INavigableWithResult<BarcodeScanResult>` and returns on the first hit. `UseSpineScanner()` registers it (a `SpineModule`).
- **Strings** (en + sv): title, torch, permission-denied and no-camera text.

### Showcase: "Barcodes" page
1. **Generate:** text, format picker, options (error correction, light on dark, dots); the code updates live.
2. **Word clock:** a 12 × 12 letter grid, drawn by the sample, showing a random 10-digit pairing code as Data Matrix 12 × 12, with the answer printed next to it and a "New code" button.
3. **Scan:** opens `BarcodeScannerPage` with the standard formats plus the 12 × 12 light grid and shows the result.

The sample gets `NSCameraUsageDescription` and `android.permission.CAMERA`.

### Tests: `tests/Plugin.Maui.Spine.Barcodes.Tests`
- Encode → decode round trip for every format, rendering the matrix to luminance and reading it with ZXing.
- A forced 12 × 12 Data Matrix, and a payload that does not fit fails loudly.
- `LightGridReader` on synthetic letter grids drawn with SkiaSharp: straight, with perspective, in night mode, with a reflection, and with no code (must give no result).
- No real photos in the repo.

### Docs and lists
- A package README for each new package, and `docs/wiki/barcodes.md`.
- Update `README.md` and `docs/wiki/packages.md` (package count, tables, dependency graph, build assets).
- Update the skills `spine-controls` and `spine-setup`.
- Icons in `assets/icons` and `assets/logo-src` (plus their table).
- Add both packages to `Spine.slnx` and `Spine.Packages.slnf`, and the test project to `ci.yml`.

## Open Questions

## Changes

- **`Plugin.Maui.Spine.Barcodes`** (new package; net10.0 plus the MAUI TFMs):
  - `Barcode.Encode` / `BarcodeMatrix` / `ToSvg`, with `QrCodeOptions`, `DataMatrixOptions` (fixed `Size`), `AztecOptions` and `Pdf417Options`. `BarcodeEncodingException` names the format, the length and the requested size.
  - `BarcodeView`, a `GraphicsView`: square or dot modules, `IsInverted`, `Error`.
  - `LightGridReader`: the spike's engine, now internal as `LightGridEngine`, behind a public reader that owns its scratch memory, takes a luminance span with a stride, and returns `LightGridResult` (text, corners, cells, diagnostics).
- **`Plugin.Maui.Spine.Scanner`** (new package):
  - `BarcodeScannerView`: formats, light grid, scanning, torch, `Detected` / `DetectedCommand` with a repeat interval, `Problem` / `ProblemChanged`.
  - Apple handler: `AVCaptureSession`, Vision for the standard formats, the Y plane to the light-grid reader, a watchdog, session notifications, and a check that `NSCameraUsageDescription` exists.
  - Android handler: CameraX preview + analysis bound to the activity, ML Kit, the Y plane to the light-grid reader, and a watchdog. `CAMERA` is declared by the package.
  - `BarcodeScannerPage`: a full-screen sheet with a torch page action that returns `BarcodeScanResult`.
  - `UseSpineScanner`: a `SpineModule`.
  - Strings in en and sv.
- **Tests:** `tests/Plugin.Maui.Spine.Barcodes.Tests`, 35 tests:
  - every format round trips;
  - fixed 12 × 12, and a clear error when the payload is too long;
  - SVG;
  - the light grid straight, at an angle, in night mode, nearly as bright, through a reflection, rotated, showing the time instead of a code, as an empty frame, with a padded stride, across frames, and refusing unsupported sizes.
- **Showcase:** a Barcodes page (generator, word clock with its code, scan sheet). `NSCameraUsageDescription` added on iOS and Mac Catalyst, and the Android minimum raised to API 23.

## Decisions
- **The scan sheet lives in the Scanner package.** `UseSpineScanner` adds the package's assembly to `SpineOptions.Assemblies` and registers the page and its view model in DI. Spine has scanned the app's assemblies by then, but `NavigationRegistry` is built on first use, so it still sees the page. No change to Spine's core.
- **Android needs API 23 for the scanner.** CameraX 1.6 declares minSdk 23, so an app below it fails the manifest merge. The Scanner package declares 23 and the Showcase raised its Android minimum to match. The Barcodes package stays at 21.
- **The Scanner package declares `android.permission.CAMERA` itself,** with `[assembly: UsesPermission]` and `camera.any` as not required, so the manifest merge adds it. iOS still needs the app's `NSCameraUsageDescription`. Without it, the Apple handler reports how to fix it instead of letting iOS kill the app.
- **The camera stops when the view leaves the screen** (`Loaded` / `Unloaded` → a handler command). It is not left to the handler being disconnected on pop, which the spike showed cannot be relied on.
- **Standard formats first, then the light grid** on iOS, where both read the same frame synchronously. On Android the light grid comes first, because ML Kit is asynchronous and holds the frame until it finishes.
- **ZXing.Net (Apache-2.0) for encoding and matrix-level Data Matrix decoding, hidden behind Spine types.** The spike showed it forces 12 × 12 and decodes a sampled `BitMatrix` directly. Micro QR is left out, because ZXing has no support for it and Data Matrix 12 × 12 fits the clock better.
- **Platform engines for standard codes: Vision on iOS, ML Kit on Android.** `LightGridReader` runs next to them on the same frames, only when `LightGrid` is set.
- **Barcodes and Scanner are separate packages,** so an app that only shows codes needs no camera permission and no ML Kit.
