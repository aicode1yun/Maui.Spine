namespace MauiSpineSampleApp.Pages.Sheets;

public partial class LateActionSheetViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Status { get; set; } = "Loading…";

    // Shown only once there is something to share, the way a scanner shows its torch once the camera has started
    public override async Task OnAppearingAsync(NavigationDirection navigationDirection)
    {
        await Task.Delay(300);
        PageActions.First(a => a.Command == ShareCommand).IsVisible = true;
        Status = "Ready to share.";
    }

    [PageAction(Svg = "share.svg", IsVisible = false)]
    [RelayCommand]
    private void Share() => Status = "Shared.";
}
