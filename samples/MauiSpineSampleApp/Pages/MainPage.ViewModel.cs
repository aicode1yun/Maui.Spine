using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MauiSpineSampleApp.Pages;

public partial class MainPageViewModel(INavigationService _navigation) : SampleViewModel
{

    // The collapsed hero header keeps room for the theme button, which sits where the header bar's
    // buttons sit on every other page: the status-bar inset down, the page margin in from the edge.
    // The collapsed hero: a bar below the status bar with the title and the theme button centred on one line.
    private const double CompactBar = 36;

    public double HeaderMinHeight => SystemBarInsets.Top + CompactBar;

    // Tall enough that the photo's S starts below the status bar (and the Dynamic Island) rather than behind it.
    public double HeaderMaxHeight => SystemBarInsets.Top + 270;

    public Thickness GearMargin => DeviceInfo.Platform == DevicePlatform.WinUI
        ? new Thickness(0, 0, 144, 0)
        : new Thickness(0, SystemBarInsets.Top + (CompactBar - 44) / 2, HeaderBarConstants.PageMargin + SystemBarInsets.Right, 0);

    // The photo runs edge to edge; the title and the rows keep clear of the Dynamic Island and the
    // rounded corners in landscape.
    public Thickness TitleMargin => new(HeaderBarConstants.PageMargin + SystemBarInsets.Left, -4, HeaderBarConstants.PageMargin, -4);

    public Thickness ListMargin => new(SystemBarInsets.Left, 0, SystemBarInsets.Right, 0);

    public double FooterHeight => SystemBarInsets.Bottom;

    // Filled before the page appears, so the first frame already has the rows and their icons.
    public ObservableCollection<Item> Items { get; } = new(SampleIndex.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase));

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(SystemBarInsets))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HeaderMinHeight)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HeaderMaxHeight)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(GearMargin)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(TitleMargin)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ListMargin)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(FooterHeight)));
        }
    }

    [RelayCommand]
    private async Task ItemTapped(Item item)
    {
        if (item.Open is { } open)
            await open(_navigation);
    }

    // One row per sample page. Add a page here when it gets a page of its own; the list shows them by title.
    private static IEnumerable<Item> SampleIndex =>
    [
        new("Bottom sheets", "Native sheets: heights, dim or blur, a guard against closing, a checkmark and an X in the header, a footer action", "sheet.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<Sheets.SheetsPage>()),
        new("Parameters and results", "Hand a page typed data, and await what it returns", "return.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<Results.ResultsPage>()),
        new("Page binding", "{PageCommand} and {PageBinding}: reach the page's view model from inside a row template", "link.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<PageBinding.PageBindingPage>()),
        new("Header bar", "Layout, large title, background, foreground and status bar: try any combination and see the code for it", "headerbar.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<HeaderBar.HeaderBarPage>()),
        new("Materials", "Blur, Liquid Glass or a tint behind any Border, from a preset or tuned by hand, and glass buttons that merge", "layers.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<Materials.MaterialsPage>()),
        new("Loading states", "TaskState and StateView: a spinner, the error with a retry, an empty line or a skeleton, from one load that lives with the page", "hourglass.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<State.StatePage>()),
        new("Page lifetime", "Poll, WhileVisible and PageLifetime: work that refreshes, listens and cancels with the page", "refresh.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<Lifetime.LifetimePage>()),
        new("Menu buttons", "Native menus from a header action, a pop-up button that shows its pick, and an icon button with sections, submenus and toggles", "more.svg", "Plugin.Maui.Spine, Plugin.Maui.Spine.Svg.Icons", n => n.NavigateToAsync<Menus.MenusPage>()),
        new("Page actions", "A header button from a command; change its text, badge, enabled state and visibility live, or replace Back", "energy.svg", "Plugin.Maui.Spine, Plugin.Maui.Spine.Svg.Icons", n => n.NavigateToAsync<PageActions.PageActionsPage>()),
        new("Haptics", "Success, warning, error, selection and impacts from the platform's own generators, on a tap, a header action, a tab switch or a sheet", "phone.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<Haptics.HapticsPage>()),
        new("Liquid Glass", "Glass.Style turns a Button or ImageButton into Liquid Glass on iOS 26; the same markup is a normal button elsewhere", "water.svg", "Plugin.Maui.Spine, Plugin.Maui.Spine.Svg", n => n.NavigateToAsync<Glass.GlassPage>()),
        new("Scroll inset", "A list that runs behind the home indicator yet scrolls its last row clear, per page, per list or app-wide", "vertical.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<ScrollInset.ScrollInsetPage>()),
        new("Typography", "Digits that keep their width, and capitals centred in badges", "edit.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<Typography.TypographyPage>()),
        new("AnimatedLabel", "A line that scrolls when the text is too long, and fades when it changes", "text.svg", "Plugin.Maui.Spine.Controls.AnimatedLabel", n => n.NavigateToAsync<Marquee.MarqueePage>()),
        new("Barcodes", "QR, Data Matrix and linear codes from any text, a pairing code on a 12 × 12 word clock, and a camera scanner that reads both", "qrcode.svg", "Plugin.Maui.Spine.Barcodes, Plugin.Maui.Spine.Scanner", n => n.NavigateToAsync<Barcodes.BarcodesPage>()),
        new("Calendar", "Month calendar to pick a date, with days marked from your own service", "calendar.svg", "Plugin.Maui.Spine.Controls.Calendar", n => n.NavigateToAsync<Dates.DatesPage>()),
        new("DataGrid", "One set of columns, a layout per width: a table when wide, two-line rows on a phone", "grid.svg", "Plugin.Maui.Spine.Controls.DataGrid, Plugin.Maui.Spine.Svg.Icons", n => n.NavigateToAsync<DataGrid.DataGridPage>()),
        new("Rows", "Settings and key/value rows in one control, any view as a button with press feedback, one screen-reader element per row", "list.svg", "Plugin.Maui.Spine.Controls.Rows, Plugin.Maui.Spine", n => n.NavigateToAsync<Rows.RowsPage>()),
        new("Shimmer", "Loading placeholders: a shimmer over empty blocks, or the real layout as its own skeleton", "lightstrip.svg", "Plugin.Maui.Spine.Controls.Shimmer", n => n.NavigateToAsync<Shimmer.ShimmerPage>()),
        new("Theming", "Light, dark or the system, an app-wide accent, colours that follow the theme, a repaint hook for code-drawn views", "theme.svg", "Plugin.Maui.Spine", n => n.NavigateToAsync<Theme.ThemePage>()),
        new("Widgets", "A home-screen widget and a Live Activity written in C#", "stack.svg", "Plugin.Maui.Spine.Widgets", n => n.NavigateToAsync<Widgets.WidgetsPage>()),
        new("Strings", "Text per language from embedded XML: values and plurals in XAML, the same store from C#, a live language switch", "globe.svg", "Plugin.Maui.Spine, Plugin.Maui.Spine.Common", n => n.NavigateToAsync<Strings.StringsPage>()),
        new("SVG icons", "Sharp, theme-tinted icons from SVG: tint only the outline, dark tones for coloured art, line weight per size, 220 bundled icons", "fish.svg", "Plugin.Maui.Spine.Svg, Plugin.Maui.Spine.Svg.Icons", n => n.NavigateToAsync<SvgIcons.SvgIconsPage>()),
    ];
}

public partial class Item : ObservableObject
{
    public Item() { }

    public Item(string title, string description, string icon, string packages, Func<INavigationService, Task> open)
    {
        Title = title;
        Description = description;
        Icon = icon;
        Packages = packages;
        Open = open;
    }

    public Func<INavigationService, Task>? Open { get; init; }

    /// <summary>Comma-separated NuGet ids the sample depends on.</summary>
    public string? Packages { get; init; }

    [ObservableProperty]
    public partial string? Icon { get; set; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial bool IsMovable { get; set; }
}