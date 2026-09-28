using Microsoft.Extensions.DependencyInjection;

namespace MauiSpineSampleApp.Pages;

/// <summary>
/// The view model every sample page derives from: the Example / Code switch at the top of the
/// page, and the header menu that picks the theme and the accent from any page.
/// </summary>
public abstract partial class SampleViewModel : ViewModelBase
{
    private readonly IThemeService _theme = IPlatformApplication.Current!.Services.GetRequiredService<IThemeService>();
    private readonly MenuPicker _themePicker;
    private readonly MenuPicker _accentPicker;

    protected SampleViewModel()
    {
        _themePicker = new MenuPicker(PickThemeCommand)
        {
            new MenuAction("System", "auto.svg") { CommandParameter = AppTheme.Unspecified },
            new MenuAction("Light", "weather1.svg") { CommandParameter = AppTheme.Light },
            new MenuAction("Dark", "weather1n.svg") { CommandParameter = AppTheme.Dark },
        };

        _accentPicker = new MenuPicker(PickAccentCommand);
        foreach (var accent in SampleAccent.All)
            _accentPicker.Add(new MenuAction(accent.Name) { CommandParameter = accent });

        ThemeMenu =
        [
            new MenuSection("Appearance") { _themePicker },
            new SubMenu("Accent", "palette.svg") { _accentPicker },
        ];

        PageActions.Add(new PageAction(null, ThemeMenu) { Svg = "palette.svg", Description = "Theme and accent" });

        // Another page may have changed the theme while this one was in the back stack.
        WhileVisible(() => { SyncThemeMenu(); _theme.Changed += OnThemeChanged; }, () => _theme.Changed -= OnThemeChanged);
        SyncThemeMenu();
    }

    /// <summary>The theme and accent menu; the header action opens it, and a page without a header bar can bind a MenuButton to it.</summary>
    public MenuItems ThemeMenu { get; }

    /// <summary>Whether the page shows its examples' code instead of the examples.</summary>
    [ObservableProperty]
    public partial bool ShowCode { get; set; }

    /// <summary>Opens <see cref="OptionsSheet"/> with an example's settings.</summary>
    protected Task ShowOptionsAsync(string title, params IReadOnlyList<ObservableObject> options) =>
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>()
            .NavigateToAsync<OptionsSheet, SampleOptions>(new SampleOptions(title, options));

    [RelayCommand]
    private void PickTheme(AppTheme theme) => _theme.Current = theme;

    [RelayCommand]
    private void PickAccent(SampleAccent accent) => _theme.Accent = accent.Accent;

    private void OnThemeChanged(object? sender, EventArgs e) => SyncThemeMenu();

    private void SyncThemeMenu()
    {
        _themePicker.Selected = _themePicker.Items.First(a => Equals(a.CommandParameter, _theme.Current));
        _accentPicker.Selected = _accentPicker.Items.FirstOrDefault(a => Equals(((SampleAccent)a.CommandParameter!).Accent, _theme.Accent));
    }
}

/// <summary>An accent the sample offers: Apple's system colours, light and dark.</summary>
public sealed record SampleAccent(string Name, SpineAccent? Accent, Color Light, Color Dark)
{
    private SampleAccent(string name, string light, string dark)
        : this(name, new SpineAccent(Color.FromArgb(light), Color.FromArgb(dark)), Color.FromArgb(light), Color.FromArgb(dark))
    {
    }

    // Blue is the sample's own accent (Primary and PrimaryDark in Colors.xaml), so picking it clears IThemeService.Accent.
    public static IReadOnlyList<SampleAccent> All { get; } =
    [
        new("Blue", null, Color.FromArgb("#007AFF"), Color.FromArgb("#0A84FF")),
        new("Indigo", "#5856D6", "#5E5CE6"),
        new("Purple", "#AF52DE", "#BF5AF2"),
        new("Pink", "#FF2D55", "#FF375F"),
        new("Red", "#FF3B30", "#FF453A"),
        new("Orange", "#FF9500", "#FF9F0A"),
        new("Yellow", "#FFCC00", "#FFD60A"),
        new("Green", "#34C759", "#30D158"),
        new("Mint", "#00C7BE", "#63E6E2"),
        new("Teal", "#30B0C7", "#40CBE0"),
    ];
}
