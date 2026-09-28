namespace MauiSpineSampleApp.Pages;

// No dimming and a medium detent: the example stays in view above the sheet and changes as you pick.
[NavigableSheet(
    Title = "Options",
    BackgroundPageOverlay = BackgroundPageOverlay.None,
    AllowedDetents = [SheetDetent.Medium, SheetDetent.FullScreen])]
public partial class OptionsSheet : INavigableWithParameter<SampleOptions>
{
    public OptionsSheet() => InitializeComponent();
}
