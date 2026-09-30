using Plugin.Maui.Spine.Barcodes;
using Plugin.Maui.Spine.Scanner;

namespace MauiSpineSampleApp.Pages.Barcodes;

public partial class BarcodesPageViewModel(INavigationService _navigation) : SampleViewModel
{
    // A value each format accepts, so switching format always shows a code
    private static readonly Dictionary<BarcodeFormat, string> SampleValues = new()
    {
        [BarcodeFormat.QrCode] = "https://github.com/jonatansoderberg/Maui.Spine",
        [BarcodeFormat.DataMatrix] = "Spine 2026",
        [BarcodeFormat.Aztec] = "Plugin.Maui.Spine.Barcodes",
        [BarcodeFormat.Pdf417] = "Spine PDF417",
        [BarcodeFormat.Code128] = "SPINE-128",
        [BarcodeFormat.Code39] = "SPINE39",
        [BarcodeFormat.Code93] = "SPINE93",
        [BarcodeFormat.Ean13] = "590123412345",
        [BarcodeFormat.Ean8] = "9638507",
        [BarcodeFormat.UpcA] = "03600029145",
        [BarcodeFormat.UpcE] = "0123456",
        [BarcodeFormat.Itf] = "12345678",
        [BarcodeFormat.Codabar] = "A40156B",
    };

    public IReadOnlyList<BarcodeFormat> Formats { get; } = [.. SampleValues.Keys];

    [ObservableProperty]
    public partial BarcodeFormat Format { get; set; } = BarcodeFormat.QrCode;

    [ObservableProperty]
    public partial string Value { get; set; } = SampleValues[BarcodeFormat.QrCode];

    [ObservableProperty]
    public partial bool IsInverted { get; set; }

    [ObservableProperty]
    public partial bool UseDots { get; set; }

    public ModuleShape Shape => UseDots ? ModuleShape.Dot : ModuleShape.Square;

    partial void OnUseDotsChanged(bool value) => OnPropertyChanged(nameof(Shape));

    partial void OnFormatChanged(BarcodeFormat value) => Value = SampleValues[value];

    [RelayCommand]
    private Task ShowCodeOptions() => ShowOptionsAsync("BarcodeView",
        new ToggleOption("Light on dark", "IsInverted: a light code on a dark background, as a display shows it.", () => IsInverted, v => IsInverted = v),
        new ToggleOption("Dots", "ModuleShape.Dot: a round dot per module.", () => UseDots, v => UseDots = v));

    // The word clock: ten random digits as a 12 × 12 Data Matrix, the size of the letter grid
    [ObservableProperty]
    public partial string PairingCode { get; set; } = NewPairingCode();

    [ObservableProperty]
    public partial BarcodeMatrix ClockMatrix { get; set; } = Barcode.Encode("0", new DataMatrixOptions { Size = (12, 12) });

    partial void OnPairingCodeChanged(string value) => ClockMatrix = Barcode.Encode(value, new DataMatrixOptions { Size = (12, 12) });

    [RelayCommand]
    private void NextPairingCode() => PairingCode = NewPairingCode();

    private static string NewPairingCode() => Random.Shared.NextInt64(0, 10_000_000_000).ToString("D10");

    [ObservableProperty]
    public partial string ScanResult { get; set; } = "Nothing scanned yet";

    [ObservableProperty]
    public partial bool ShowScanDiagnostics { get; set; }

    [ObservableProperty]
    public partial bool ShowTorch { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowReticle { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowPrompt { get; set; }

    [ObservableProperty]
    public partial bool ShowDetection { get; set; } = true;

    [ObservableProperty]
    public partial bool PlaySound { get; set; } = true;

    [ObservableProperty]
    public partial string OpensAt { get; set; } = SheetDetent.Medium;

    private static readonly (string Label, string Detent, string Description)[] Sizes =
    [
        ("Half", SheetDetent.Medium, "Medium: half the screen, and it can be pulled up to full screen."),
        ("Large", SheetDetent.Expanded, "Expanded: most of the screen, the page still showing above."),
        ("Full screen", SheetDetent.FullScreen, "FullScreen: the whole screen."),
    ];

    [RelayCommand]
    private Task ShowScanOptions()
    {
        var size = new ChoiceGroup("Opens at",
            [.. Sizes.Select(s => new Choice(s.Label, s.Description, () => OpensAt = s.Detent))]);
        size.Select(Array.FindIndex(Sizes, s => s.Detent == OpensAt));

        return ShowOptionsAsync("Scanner",
            size,
            new ToggleOption("Aim corners", "ShowReticle: white corners that breathe where to aim; wide for linear codes.", () => ShowReticle, v => ShowReticle = v),
            new ToggleOption("Show the hit", "ShowDetection: the frame stops and the marked code bursts towards you before the sheet closes.", () => ShowDetection, v => ShowDetection = v),
            new ToggleOption("Sound", "PlaySound: a short sound on a hit, muted by the silent switch on iOS.", () => PlaySound, v => PlaySound = v),
            new ToggleOption("Prompt", "ShowPrompt: a text box at the bottom with Prompt.", () => ShowPrompt, v => ShowPrompt = v),
            new ToggleOption("Torch", "ShowTorch: a torch button in the header when the camera has one.", () => ShowTorch, v => ShowTorch = v),
            new ToggleOption("Diagnostics", "ShowDiagnostics: frames per second, time per frame and what the light-grid reader sees.", () => ShowScanDiagnostics, v => ShowScanDiagnostics = v));
    }

    [RelayCommand]
    private async Task Scan()
    {
        var result = await _navigation.NavigateToWithResultAsync<BarcodeScannerPage, BarcodeScanOptions, BarcodeScanResult>(
            new BarcodeScanOptions
            {
                Formats = BarcodeFormat.All,
                LightGrid = new LightGridOptions(12, 12),
                ShowReticle = ShowReticle,
                ShowDetection = ShowDetection,
                PlaySound = PlaySound,
                ShowPrompt = ShowPrompt,
                Prompt = "Point the camera at a code",
                ShowTorch = ShowTorch,
                ShowDiagnostics = ShowScanDiagnostics,
                // The sheet can always be pulled to full screen from where it opens
                Detents = OpensAt == SheetDetent.FullScreen ? [SheetDetent.FullScreen] : [OpensAt, SheetDetent.FullScreen],
            });
        ScanResult = result switch
        {
            { IsSuccess: true, Value: { IsLightGrid: true } code } => $"Word clock: {code.Value}",
            { IsSuccess: true, Value: { } code } => $"{code.Format}: {code.Value}",
            _ => "Canceled",
        };
    }

    public override Task OnAppearingAsync(NavigationDirection navigationDirection)
    {
        ClockMatrix = Barcode.Encode(PairingCode, new DataMatrixOptions { Size = (12, 12) });
        return base.OnAppearingAsync(navigationDirection);
    }
}
