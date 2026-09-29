using MauiSpineSampleApp.Widgets.Hockey;

namespace MauiSpineSampleApp.Pages.LiveActivities;

public partial class LiveActivitiesPageViewModel : SampleViewModel
{
    public LiveActivitiesPageViewModel(LiveScore score)
    {
        Score = score;

        // The in-app clock runs while the page shows; the countdown reaching face-off starts the game.
        Poll(TimeSpan.FromSeconds(1), _ => Score.TickAsync());
    }

    public LiveScore Score { get; }
}
