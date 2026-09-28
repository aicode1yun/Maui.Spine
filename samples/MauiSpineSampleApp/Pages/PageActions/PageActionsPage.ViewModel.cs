namespace MauiSpineSampleApp.Pages.PageActions;

public partial class PageActionsPageViewModel : SampleViewModel
{
    private int _count;
    private PageAction? _cancel;

    public PageActionsPageViewModel()
    {
        // The header bar has one trailing slot, and it goes to the first visible Secondary action:
        // the base class's theme menu would take it from the actions this page is about.
        PageActions.Remove(PageActions.Single(a => a.Menu == ThemeMenu));
    }

    [ObservableProperty]
    public partial string Log { get; set; } = "Tap Filter at the top right.";

    [ObservableProperty]
    public partial string CancelLabel { get; set; } = "Show Cancel instead of Back";

    // Declared once by the attribute; nothing to add in OnAppearingAsync.
    [PageAction("Filter", Order = 0)]
    [RelayCommand]
    private void Filter() => Log = $"Filter tapped at {DateTime.Now:HH:mm:ss}";

    // Second in the Secondary slot: takes over when Filter hides itself.
    [PageAction(Svg = "bell.svg", Order = 1)]
    [RelayCommand]
    private void Bell() => Log = $"Bell tapped at {DateTime.Now:HH:mm:ss}";

    private PageAction FilterAction => PageActions.First(a => a.Command == FilterCommand);

    [RelayCommand]
    private Task ShowFilterOptions() => ShowOptionsAsync("Filter",
        new ToggleOption("Enabled", "Off, the button greys out and takes no taps.", () => FilterAction.IsEnabled, value => FilterAction.IsEnabled = value),
        new ToggleOption("Visible", "Off, Filter leaves the header, and Bell, the next Secondary action, takes its place.", () => FilterAction.IsVisible, value => FilterAction.IsVisible = value));

    [RelayCommand]
    private void CountUp()
    {
        _count++;
        FilterAction.Text = $"Filter ({_count})";
        FilterAction.Badge = _count.ToString();
    }

    [RelayCommand]
    private void ClearCount()
    {
        _count = 0;
        FilterAction.Text = "Filter";
        FilterAction.Badge = null;
    }

    // Adding and removing at runtime works too; a Primary action replaces the back button.
    [RelayCommand]
    private void ToggleCancel()
    {
        if (_cancel is null)
        {
            _cancel = new PageAction("Cancel", ToggleCancelCommand) { Placement = PageActionPlacement.Primary };
            PageActions.Add(_cancel);
            CancelLabel = "Show Back again";
        }
        else
        {
            PageActions.Remove(_cancel);
            _cancel = null;
            CancelLabel = "Show Cancel instead of Back";
        }
    }
}
