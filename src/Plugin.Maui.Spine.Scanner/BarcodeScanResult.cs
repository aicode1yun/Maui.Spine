using Plugin.Maui.Spine.Barcodes;
using Plugin.Maui.Spine.Core;

namespace Plugin.Maui.Spine.Scanner;

/// <summary>A code the camera read.</summary>
/// <param name="Value">The decoded text.</param>
/// <param name="Format">The symbology it was read as.</param>
/// <param name="IsLightGrid">Read by the light-grid reader (a code shown by lamps), not the platform's barcode reader.</param>
public sealed record BarcodeScanResult(string Value, BarcodeFormat Format, bool IsLightGrid = false)
{
    /// <summary>
    /// The code's corners in the <see cref="BarcodeScannerView"/>, in device-independent units, in the order the reader
    /// found them (top-left, top-right, bottom-right, bottom-left of the code); empty when the reader gave none.
    /// </summary>
    public IReadOnlyList<Point> Corners { get; init; } = [];

    /// <summary>For a light grid, its lamps across and down, to draw the grid over <see cref="Corners"/>.</summary>
    public (int Columns, int Rows)? Grid { get; init; }

    /// <summary>For a light grid, which lamps were lit, [column, row], as the reader saw them.</summary>
    internal bool[,]? Cells { get; init; }
}

/// <summary>A hit from a frame, before its corners are placed in the view: pixels in the analysed image.</summary>
internal sealed record FrameHit(BarcodeScanResult Result, System.Numerics.Vector2[] Corners, float ImageWidth, float ImageHeight);

/// <summary>What <see cref="BarcodeScannerPage"/> looks for, how it looks and how it opens.</summary>
public sealed record BarcodeScanOptions
{
    /// <summary>The standard symbologies to read; <see cref="BarcodeFormat.None"/> reads only <see cref="LightGrid"/>.</summary>
    public BarcodeFormat Formats { get; init; } = BarcodeFormat.All;

    /// <summary>A grid of lamps to read as well, such as <c>new LightGridOptions(12, 12)</c> for a word clock.</summary>
    public LightGridOptions? LightGrid { get; init; }

    /// <summary>The sheet's title, or <see langword="null"/> for the localised "Scan code".</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Corners in the accent colour that pulse around the area to aim at: square for 2D codes and light grids,
    /// wide when <see cref="Formats"/> holds only linear codes. On by default.
    /// </summary>
    public bool ShowReticle { get; init; } = true;

    /// <summary>A text box at the bottom with <see cref="Prompt"/>; problems are shown there too. Off by default.</summary>
    public bool ShowPrompt { get; init; }

    /// <summary>The prompt's text, or <see langword="null"/> for the localised "Point the camera at the code".</summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// On a hit, the frame that was read stops, and the code, marked in the accent colour (with its grid for a light
    /// grid), bursts towards the user while the frame fades; the sheet closes when that has finished, about half a
    /// second later. On by default; off returns at once.
    /// </summary>
    public bool ShowDetection { get; init; } = true;

    /// <summary>A torch button in the header when the camera has one. On by default.</summary>
    public bool ShowTorch { get; init; } = true;

    /// <summary>
    /// A short sound on a hit, next to the success haptic: the "Tink" system sound on iOS (muted by the silent switch)
    /// and the acknowledge tone on Android. On by default.
    /// </summary>
    public bool PlaySound { get; init; } = true;

    /// <summary>
    /// The sheet sizes the user can drag between, as <see cref="SheetDetent"/> names or percentages such as <c>"50%"</c>.
    /// Medium (half height) and full screen by default.
    /// </summary>
    public IReadOnlyList<string> Detents { get; init; } = [SheetDetent.Medium, SheetDetent.FullScreen];

    /// <summary>The size the sheet opens at, or <see langword="null"/> for the first of <see cref="Detents"/>.</summary>
    public string? InitialDetent { get; init; }

    /// <summary>Shows <see cref="BarcodeScannerView.Diagnostics"/> at the bottom, to see why a code is not read.</summary>
    public bool ShowDiagnostics { get; init; }
}

/// <summary>Why a <see cref="BarcodeScannerView"/> is not scanning.</summary>
public enum ScannerProblem
{
    /// <summary>The user said no to the camera, or turned it off in Settings.</summary>
    PermissionDenied,

    /// <summary>No camera, or none the scanner can use.</summary>
    NoCamera,

    /// <summary>The system took the camera away: a call, another app, split view.</summary>
    Interrupted,

    /// <summary>No frame for two seconds; the scanner is restarting the camera.</summary>
    NoFrames,

    /// <summary>Anything else; <see cref="ScannerProblemEventArgs.Message"/> has the details.</summary>
    Failed,
}

public sealed class BarcodeDetectedEventArgs(BarcodeScanResult result) : EventArgs
{
    public BarcodeScanResult Result { get; } = result;
}

/// <summary>A problem started (<see cref="Problem"/> set) or cleared (<see langword="null"/>).</summary>
public sealed class ScannerProblemEventArgs(ScannerProblem? problem, string? message) : EventArgs
{
    public ScannerProblem? Problem { get; } = problem;

    /// <summary>Text to show the user, localised; for a missing platform declaration, the developer's fix.</summary>
    public string? Message { get; } = message;
}
