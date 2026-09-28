using Plugin.Maui.Spine.Common;
using Plugin.Maui.Spine.Widgets;

namespace MauiSpineSampleApp.Pages.Widgets;

public partial class WidgetsPageViewModel(IWidgetService _widgets, ILiveActivityService _liveActivities) : SampleViewModel
{
    private const string ActivityKind = "sample";

    [ObservableProperty]
    public partial string LiveActivityLabel { get; set; } = "Start live activity";

    // The running activity is asked for rather than held: an activity outlives the page, and the app.
    private LiveActivity? Activity => _liveActivities.Active.FirstOrDefault(a => a.Kind == ActivityKind);

    public override Task OnAppearingAsync(NavigationDirection navigationDirection)
    {
        LiveActivityLabel = Activity is null ? "Start live activity" : "End live activity";
        return base.OnAppearingAsync(navigationDirection);
    }

    [ObservableProperty]
    public partial string Refreshed { get; set; } = "Not refreshed yet";

    [RelayCommand]
    private async Task RefreshWidgets()
    {
        await _widgets.RefreshAllAsync();
        Refreshed = $"Refreshed at {DateTime.Now:HH:mm:ss}";
    }

    // Demo of the Live Activity API: one countdown in the Dynamic Island, ended by the same button.
    [RelayCommand]
    private async Task ToggleLiveActivity()
    {
        if (Activity is { } running)
        {
            await running.EndAsync();
            LiveActivityLabel = "Start live activity";
            return;
        }

        var start = DateTimeOffset.Now.AddMinutes(42);
        var activity = await _liveActivities.StartAsync(ActivityKind, new LiveActivityLayout
        {
            // The same surface as the sample widget; text on a fixed color gets fixed colors too.
            LockScreen = W.HStack(10,
                W.Icon("fish", WidgetColor.FromHex("#8FE3B0")),
                W.VStack(2,
                    W.Text("Product demo · Room 4B").Headline().Bold().Color(WidgetColor.FromHex("#FFFFFF")),
                    W.Text("Your next event").Caption().Color(WidgetColor.FromHex("#B3FFFFFF"))),
                W.Spacer(),
                W.Timer(start).Title().Bold().Color(WidgetColor.FromHex("#8FE3B0"))),
            Background = WidgetColor.FromHex("#1B5E3F"),
            ExpandedLeading = W.Icon("fish", WidgetColor.Green),
            ExpandedTrailing = W.Timer(start).Headline().Bold().Color(WidgetColor.Green),
            ExpandedCenter = W.Text("Product demo · Room 4B").Headline().Bold(),
            ExpandedBottom = W.VStack(W.Text("Starts 11:04 · 45 min").Caption().Secondary(), W.Progress(0.35, WidgetColor.Green)),
            CompactLeading = W.Icon("fish", WidgetColor.Green),
            CompactTrailing = W.Timer(start).Caption().Bold().Color(WidgetColor.Green),
            Minimal = W.Icon("fish", WidgetColor.Green),
            Link = _widgets.LinkFor(ActivityKind),
        }, staleAt: start.AddHours(1));

        LiveActivityLabel = activity is null ? "Live activities unavailable" : "End live activity";
    }
}
