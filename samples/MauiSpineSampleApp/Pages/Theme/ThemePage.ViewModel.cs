namespace MauiSpineSampleApp.Pages.Theme;

public partial class ThemePageViewModel : SampleViewModel
{
    private static readonly AppTheme[] Themes = [AppTheme.Unspecified, AppTheme.Light, AppTheme.Dark];

    private readonly IThemeService _theme;
    private readonly ChoiceGroup _themeChoice;
    private readonly ChoiceGroup _accentChoice;

    public ThemePageViewModel(IThemeService theme)
    {
        _theme = theme;

        _themeChoice = new("Theme",
        [
            new("System", "Follows the device, and changes with it.", () => Apply(() => _theme.Current = AppTheme.Unspecified)),
            new("Light", "Light, whatever the device is set to.", () => Apply(() => _theme.Current = AppTheme.Light)),
            new("Dark", "Dark, whatever the device is set to.", () => Apply(() => _theme.Current = AppTheme.Dark)),
        ]);

        _accentChoice = new("Accent",
        [
            .. SampleAccent.All.Select(accent => new Choice(
                accent.Name,
                accent.Accent is null ? "The app's own colour: Primary and PrimaryDark in Colors.xaml." : $"{accent.Light.ToArgbHex()} in light mode, {accent.Dark.ToArgbHex()} in dark.",
                () => Apply(() => _theme.Accent = accent.Accent))),
        ]);

        WhileVisible(() => _theme.Changed += OnThemeChanged, () => _theme.Changed -= OnThemeChanged);
        Sync();
    }

    [ObservableProperty]
    public partial string Effective { get; set; } = "";

    [ObservableProperty]
    public partial string AccentName { get; set; } = "";

    [RelayCommand]
    private Task ShowThemeOptions() => ShowOptionsAsync("Theme", _themeChoice);

    [RelayCommand]
    private Task ShowAccentOptions() => ShowOptionsAsync("Accent", _accentChoice);

    public override Task OnAppearingAsync(NavigationDirection navigationDirection)
    {
        Sync();
        return base.OnAppearingAsync(navigationDirection);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Sync();

    // The options sheet covers the page, which stops hearing Changed while it is hidden.
    private void Apply(Action change)
    {
        change();
        Sync();
    }

    private void Sync()
    {
        _themeChoice.Select(Array.IndexOf(Themes, _theme.Current));

        var accent = SampleAccent.All.FirstOrDefault(a => Equals(a.Accent, _theme.Accent));
        _accentChoice.Select(accent is null ? -1 : SampleAccent.All.ToList().IndexOf(accent));

        Effective = $"Chosen: {_themeChoice.Selected?.Label}. In effect: {_theme.Effective}.";
        AccentName = accent is null ? "Accent: a custom colour" : $"Accent: {accent.Name}";
    }
}
