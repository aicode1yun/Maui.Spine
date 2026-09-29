namespace MauiSpineSampleApp.Pages.Barcodes;

[NavigableRegion(Title = "Barcodes")]
public partial class BarcodesPage : INavigableWithParameter<OpenScanner> { public BarcodesPage() => InitializeComponent(); }

/// <summary>Opens the page with the scanner already up, as the "Scan a code" shortcut does.</summary>
public sealed record OpenScanner;
