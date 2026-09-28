using MauiSpineSampleApp.Pages.Shimmer;

namespace MauiSpineSampleApp.Pages.State;

public partial class StatePageViewModel : SampleViewModel
{
    private static readonly string[] AllNames = ["Ada", "Alan", "Grace", "Katherine", "Linus", "Margaret"];

    private static readonly PersonRow[] Loaded =
    [
        new("Ada Lovelace", "Analyst", "dotnet_bot.png"),
        new("Grace Hopper", "Compiler author", "dotnet_bot.png"),
        new("Alan Turing", "Codebreaker", "dotnet_bot.png"),
        new("Katherine Johnson", "Trajectory analyst", "dotnet_bot.png"),
    ];

    public StatePageViewModel()
    {
        // Loaded when the page appears, cancelled when it is left. Nothing in OnAppearingAsync.
        Names = Load(async ct =>
        {
            await Task.Delay(3000, ct);

            if (Fail)
                throw new HttpRequestException("503 Service Unavailable from /api/names");

            return ReturnEmpty ? [] : (IReadOnlyList<string>)AllNames;
        }, isEmpty: names => names.Count == 0);

        // Four empty rows until the real ones arrive, drawn as a skeleton.
        People = Load(async ct =>
        {
            await Task.Delay(3000, ct);

            if (FailRefresh && People!.HasResult)
                throw new HttpRequestException("The connection was lost");

            return (IReadOnlyList<PersonRow>)Loaded;
        }, placeholder: () => [.. Enumerable.Repeat(new PersonRow("", "", null), 4)]);

        Forecast = Load<string>(async ct =>
        {
            await Task.Delay(3000, ct);

            if (_forecastAttempts++ % 2 == 0)
                throw new TimeoutException("The weather service did not answer in 3 s");

            return "Sunny, 18 °C, light wind from the south-west";
        });
    }

    private int _forecastAttempts;

    public TaskState<IReadOnlyList<string>> Names { get; }

    public TaskState<IReadOnlyList<PersonRow>> People { get; }

    public TaskState<string> Forecast { get; }

    [ObservableProperty]
    public partial bool Fail { get; set; }

    [ObservableProperty]
    public partial bool ReturnEmpty { get; set; }

    [ObservableProperty]
    public partial bool FailRefresh { get; set; }

    // What the list shows has changed: start over from the spinner rather than refresh the old names.
    partial void OnFailChanged(bool value) => LoadNamesAgain();

    partial void OnReturnEmptyChanged(bool value) => LoadNamesAgain();

    private void LoadNamesAgain()
    {
        Names.Reset();
        _ = Names.LoadAsync();
    }
}
