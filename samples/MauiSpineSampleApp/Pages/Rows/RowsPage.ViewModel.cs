namespace MauiSpineSampleApp.Pages.Rows;

public partial class RowsPageViewModel : SampleViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WiFiState))]
    public partial bool WiFi { get; set; } = true;

    public string WiFiState => WiFi ? "Connected" : "Off";

    [ObservableProperty]
    public partial bool Notifications { get; set; }

    [ObservableProperty]
    public partial string Theme { get; set; } = "System";

    [ObservableProperty]
    public partial string LastTap { get; set; } = "Tap a row.";

    [ObservableProperty]
    public partial int CardTaps { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    public partial bool CanOpen { get; set; } = true;

    private static readonly string[] Themes = ["System", "Light", "Dark"];

    [RelayCommand]
    private Task ShowRowOptions() => ShowOptionsAsync("Rows",
        new ToggleOption("Rows can open", "Follows CanExecute of the rows' command: off, Account and Privacy give no press feedback and take no taps.", () => CanOpen, value => CanOpen = value));

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open(string what) => LastTap = $"Opened {what} at {DateTime.Now:HH:mm:ss}";

    [RelayCommand]
    private void CycleTheme()
    {
        Theme = Themes[(Array.IndexOf(Themes, Theme) + 1) % Themes.Length];
        LastTap = $"Theme choice: {Theme}";
    }

    [RelayCommand]
    private void TapCard() => CardTaps++;
}
