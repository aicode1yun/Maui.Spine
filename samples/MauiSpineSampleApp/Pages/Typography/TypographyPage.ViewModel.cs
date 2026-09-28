namespace MauiSpineSampleApp.Pages.Typography;

public partial class TypographyPageViewModel : SampleViewModel
{
    public TypographyPageViewModel()
    {
        Poll(TimeSpan.FromMilliseconds(100), _ =>
        {
            Clock = DateTime.Now.ToString("HH:mm:ss.f");
            return Task.CompletedTask;
        });
    }

    [ObservableProperty]
    public partial string Clock { get; set; } = "00:00:00.0";
}
