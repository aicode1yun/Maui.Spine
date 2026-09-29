namespace MauiSpineSampleApp.Pages.Marquee;

public partial class MarqueePageViewModel : SampleViewModel
{
    [ObservableProperty]
    public partial string DynamicText { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor.";

    [ObservableProperty]
    public partial double ScrollSpeed { get; set; } = 38;

    [ObservableProperty]
    public partial int PauseAtEnds { get; set; } = 2000;

    [ObservableProperty]
    public partial double FadeEdgeWidth { get; set; } = 8;

    [ObservableProperty]
    public partial bool FadeOnChange { get; set; } = true;

    [ObservableProperty]
    public partial bool RestartOnChange { get; set; }

    [ObservableProperty]
    public partial int Score { get; set; } = 98;

    [ObservableProperty]
    public partial string Countdown { get; set; } = "0:15";

    [ObservableProperty]
    public partial int RollDuration { get; set; } = 350;

    private CancellationTokenSource? _countdown;

    private static readonly string[] _latinWords =
    [
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
        "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua",
    ];

    private static readonly Random _random = new();

    [RelayCommand]
    private void ChangeDynamicText()
    {
        var words = Enumerable.Range(0, _random.Next(3, 20))
            .Select(_ => _latinWords[_random.Next(_latinWords.Length)])
            .ToList();

        words[0] = char.ToUpper(words[0][0]) + words[0][1..];
        DynamicText = string.Join(" ", words) + ".";
    }

    [RelayCommand]
    private void Add(string amount) => Score += int.Parse(amount);

    [RelayCommand]
    private async Task CountDown()
    {
        _countdown?.Cancel();
        var cts = _countdown = new CancellationTokenSource();

        for (var left = TimeSpan.FromSeconds(15); left >= TimeSpan.Zero; left -= TimeSpan.FromSeconds(1))
        {
            Countdown = $"{(int)left.TotalMinutes}:{left.Seconds:00}";
            try
            {
                await Task.Delay(1000, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [RelayCommand]
    private Task ShowScrollOptions() => ShowOptionsAsync("Scrolling",
        new SliderOption("Speed, points per second", 10, 150, () => ScrollSpeed, v => ScrollSpeed = v, "0"),
        new SliderOption("Pause at each end, ms", 0, 5000, () => PauseAtEnds, v => PauseAtEnds = (int)v, "0"),
        new SliderOption("Edge fade, points", 0, 40, () => FadeEdgeWidth, v => FadeEdgeWidth = v, "0"));

    [RelayCommand]
    private Task ShowChangeOptions() => ShowOptionsAsync("Changing text",
        new ToggleOption("Fade", "The old text fades out and the new text in.", () => FadeOnChange, v => FadeOnChange = v),
        new ToggleOption("Start over", "Scrolling restarts from the beginning of the new text. Off, it carries on from where it was.", () => RestartOnChange, v => RestartOnChange = v));

    [RelayCommand]
    private Task ShowRollOptions() => ShowOptionsAsync("Rolling",
        new SliderOption("Duration, ms", 100, 1500, () => RollDuration, v => RollDuration = (int)v, "0"));
}
