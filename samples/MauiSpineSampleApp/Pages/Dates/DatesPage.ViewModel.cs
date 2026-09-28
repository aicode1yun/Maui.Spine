using System.Globalization;
using Plugin.Maui.Spine.Controls;
using Plugin.Maui.Spine.Common;

namespace MauiSpineSampleApp.Pages.Dates;

public partial class DatesPageViewModel(ISpineStrings _strings) : SampleViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedText))]
    public partial DateTime SelectedDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    public partial DateTime DisplayDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial bool ShowWeekNumbers { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowTrailingDays { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowSelectedDate { get; set; } = true;

    [ObservableProperty]
    public partial DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>Null follows SpineStrings.Culture, the app's language.</summary>
    [ObservableProperty]
    public partial CultureInfo? Culture { get; set; }

    public string SelectedText => SelectedDate == DateTime.MinValue ? "No selection" : $"Selected: {SelectedDate:yyyy-MM-dd}";

    public string DisplayText => $"Showing: {DisplayDate:yyyy-MM}";

    [RelayCommand] private void GoToToday() => SelectedDate = DateTime.Today;

    [RelayCommand] private void ClearSelection() => SelectedDate = DateTime.MinValue;

    [RelayCommand]
    private Task ShowCalendarOptions() => ShowOptionsAsync("Calendar",
        new ToggleOption("Week numbers", "ISO 8601 week numbers down the side.", () => ShowWeekNumbers, v => ShowWeekNumbers = v),
        new ToggleOption("Days of other months", "The neighbouring months' days, dimmed, in the first and last rows.", () => ShowTrailingDays, v => ShowTrailingDays = v),
        new ToggleOption("Show the selection", "Off, a tap still sets SelectedDate, but no selection is drawn.", () => ShowSelectedDate, v => ShowSelectedDate = v),
        Choices("First day of the week", FirstDayOfWeek, v => FirstDayOfWeek = v,
            ("Monday", "Most of Europe, and ISO 8601.", DayOfWeek.Monday),
            ("Sunday", "The US, Canada and Japan.", DayOfWeek.Sunday),
            ("Saturday", "Much of the Middle East.", DayOfWeek.Saturday)),
        Choices("Language", _strings.Culture.TwoLetterISOLanguageName == "sv" ? "sv" : "en", v => _strings.Culture = CultureInfo.GetCultureInfo(v),
            ("English", "The whole app in English. A calendar without a Culture of its own follows at once.", "en"),
            ("Svenska", "The whole app in Swedish, the calendar's month and day names too.", "sv")),
        Choices<string?>("Culture", Culture?.Name, v => Culture = v is null ? null : CultureInfo.GetCultureInfo(v),
            ("App", "No Culture of its own: the names follow the app's language above.", null),
            ("en-US", "Pinned to US English, whatever the app's language.", "en-US"),
            ("sv-SE", "Pinned to Swedish.", "sv-SE"),
            ("de-DE", "Pinned to German.", "de-DE"),
            ("fi-FI", "Pinned to Finnish.", "fi-FI")));

    // ── Marked days ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkSource))]
    public partial bool ShowMarks { get; set; } = true;

    public ICalendarMarkSource? MarkSource => ShowMarks ? Events : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalendarStyle))]
    public partial CalendarMarkStyle MarkStyle { get; set; } = CalendarMarkStyle.Fill;

    // Null keeps the defaults; a StyleOptions change rebuilds the calendar.
    public CalendarStyleOptions? CalendarStyle =>
        MarkStyle == CalendarMarkStyle.Dot ? new CalendarStyleOptions { MarkStyle = CalendarMarkStyle.Dot } : null;

    [ObservableProperty]
    public partial string MarkQueryText { get; set; } = "The service has not been asked yet.";

    [RelayCommand] private void ChangeEvents() => Events.Reshuffle();

    [RelayCommand]
    private Task ShowMarkOptions() => ShowOptionsAsync("Marked days",
        new ToggleOption("Marks", "Off, the calendar has no MarkSource.", () => ShowMarks, v => ShowMarks = v),
        Choices("Style", MarkStyle, v => MarkStyle = v,
            ("Fill", "A soft accent circle behind the number.", CalendarMarkStyle.Fill),
            ("Dot", "A small dot under the number, which stays visible on today and the selected day.", CalendarMarkStyle.Dot)));

    // Choosing sets the page's property at once, so the calendar changes behind the sheet.
    private static ChoiceGroup Choices<T>(string name, T current, Action<T> set, params (string Label, string Description, T Value)[] items)
    {
        ChoiceGroup group = null!;
        group = new ChoiceGroup(name, [.. items.Select((item, i) => new Choice(item.Label, item.Description, () =>
        {
            set(item.Value);
            group.Select(i);
        }))]);
        group.Select(Array.FindIndex(items, item => EqualityComparer<T>.Default.Equals(item.Value, current)));
        return group;
    }

    // The service lives as long as this view model, so the counter needs no unsubscribing.
    private FakeEventService Events => field ??= new FakeEventService((first, last, count) =>
        MainThread.BeginInvokeOnMainThread(() => MarkQueryText = $"Asked {count}×, last for {first:MMM d} – {last:MMM d}"));

    /// <summary>
    /// Stands in for an app's own service: it knows the "events", the calendar only asks which days
    /// have one. Pseudo-random days per month, answered after a short delay like a network call.
    /// </summary>
    private sealed class FakeEventService(Action<DateOnly, DateOnly, int> asked) : ICalendarMarkSource
    {
        private int _seed = 1;
        private int _count;

        public event EventHandler? Changed;

        public async ValueTask<IReadOnlyCollection<DateOnly>> GetMarkedDatesAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken)
        {
            asked(first, last, Interlocked.Increment(ref _count));
            await Task.Delay(300, cancellationToken);

            // Today always has something, so the today-and-marked look is on show.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var marked = new List<DateOnly>();
            for (var day = first; day <= last; day = day.AddDays(1))
            {
                if (day == today || Scatter(day.DayNumber, _seed) % 5 == 0)
                    marked.Add(day);
            }
            return marked;
        }

        /// <summary>New data: the calendar asks again for the months it shows.</summary>
        public void Reshuffle()
        {
            _seed++;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // Stable across runs, unlike HashCode.Combine.
        private static uint Scatter(int day, int seed)
        {
            var h = (uint)day * 2654435761u ^ (uint)seed * 40503u;
            h ^= h >> 15;
            h *= 2246822519u;
            return h ^ (h >> 13);
        }
    }
}
