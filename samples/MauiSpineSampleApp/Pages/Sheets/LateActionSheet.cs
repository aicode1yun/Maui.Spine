namespace MauiSpineSampleApp.Pages.Sheets;

[NavigableSheet(
    Title = "Photo",
    AllowedDetents = [SheetDetent.Medium, SheetDetent.FullScreen])]
public partial class LateActionSheet
{
    public LateActionSheet() => InitializeComponent();
}
