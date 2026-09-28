namespace MauiSpineSampleApp.Pages.Menus;

public partial class MenusPageViewModel : SampleViewModel
{
    [ObservableProperty]
    public partial string Sorted { get; set; } = "Sorted by name";

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    private readonly MenuPicker _filter;
    private readonly MenuAction _photos = new("Photos", "cam.svg") { IsChecked = true, KeepsMenuOpen = true };
    private readonly MenuAction _videos = new("Videos", "play.svg") { KeepsMenuOpen = true };

    // A pop-up button: the button text follows the picked row.
    public MenuItems SortMenu { get; }

    // An icon button: sections, a picker, a submenu with toggles, a destructive row.
    public MenuItems MoreMenu { get; }

    public MenusPageViewModel()
    {
        SortMenu =
        [
            new MenuPicker(SortCommand)
            {
                new MenuAction("Name") { IsChecked = true },
                new MenuAction("Date"),
                new MenuAction("Size") { IsEnabled = false },
            },
        ];

        _filter = new MenuPicker(FilterCommand)
        {
            new MenuAction("All items", "house.svg") { IsChecked = true },
            new MenuAction("Favourites", "star.svg"),
            new MenuAction("Edited", "edit.svg"),
        };

        MoreMenu =
        [
            new MenuSection("Filter:") { _filter },
            new MenuSection
            {
                new SubMenu("Media types", "image.svg") { _photos, _videos },
                new MenuAction("Reset", "refresh.svg", ResetCommand) { IsDestructive = true },
            },
        ];

        _photos.PropertyChanged += (_, _) => Summarize();
        _videos.PropertyChanged += (_, _) => Summarize();
        Summarize();
    }

    [RelayCommand]
    private void Sort(MenuAction action) => Sorted = $"Sorted by {action.Title.ToLowerInvariant()}";

    [RelayCommand]
    private void Filter() => Summarize();

    [RelayCommand]
    private void Reset()
    {
        _filter.Selected = _filter.Items[0];
        _photos.IsChecked = true;
        _videos.IsChecked = false;
        Summarize();
    }

    private void Summarize()
    {
        var media = new[] { _photos, _videos }.Where(a => a.IsChecked).Select(a => a.Title.ToLowerInvariant()).ToList();
        Summary = $"Showing {_filter.Selected?.Title.ToLowerInvariant()}: {(media.Count > 0 ? string.Join(" and ", media) : "no media")}";
    }
}
