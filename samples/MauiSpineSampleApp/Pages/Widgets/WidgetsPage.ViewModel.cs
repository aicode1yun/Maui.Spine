using MauiSpineSampleApp.Widgets.Hockey;
using Plugin.Maui.Spine.Common;

namespace MauiSpineSampleApp.Pages.Widgets;

public partial class WidgetsPageViewModel : SampleViewModel
{
    private readonly IWidgetService _widgets;

    public WidgetsPageViewModel(IWidgetService widgets, LiveScore score)
    {
        _widgets = widgets;
        Score = score;

        Poll(TimeSpan.FromSeconds(1), _ => Score.TickAsync());
    }

    /// <summary>The same game as on the Live Activities page: a goal here changes both.</summary>
    public LiveScore Score { get; }

    [ObservableProperty]
    public partial string Refreshed { get; set; } = "Not refreshed yet";

    [RelayCommand]
    private async Task RefreshWidgets()
    {
        await _widgets.RefreshAllAsync();
        Refreshed = $"Refreshed at {DateTime.Now:HH:mm:ss}";
    }
}
