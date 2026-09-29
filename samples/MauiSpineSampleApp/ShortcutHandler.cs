using MauiSpineSampleApp.Pages.Barcodes;
using MauiSpineSampleApp.Pages.LiveActivities;
using MauiSpineSampleApp.Pages.Shortcuts;
using MauiSpineSampleApp.Pages.Theme;
using MauiSpineSampleApp.Pages.Widgets;

namespace MauiBottomSheetPoc;

/// <summary>
/// The app's shortcuts: the long-press menu on the app icon (Android, iOS), the jump list (Windows) and
/// the tray menu (Windows, Mac). A singleton, so it can say which shortcut opened the app last.
/// </summary>
public class ShortcutHandler(INavigationService _navigation) : IShortcutHandler
{
    public static IReadOnlyList<SpineShortcut> All { get; } =
    [
        new("live-score", "Live score", Icon: "timer", Subtitle: "Owls vs Foxes"),
        new("widgets", "Widgets", Icon: "stack", Subtitle: "Home screen and Lock Screen"),
        // A camera from the menu bar makes little sense on a Mac, so it stays out of the tray.
        new("scan", "Scan a code", ShowInTray: false, Icon: "qrcode", Subtitle: "Opens the camera"),
        new("theme", "Theme", Icon: "theme", Subtitle: "Light, dark or the system"),
    ];

    public static void Configure(IShortcutBuilder builder)
    {
        foreach (var shortcut in All)
            builder.Add(shortcut.Id, shortcut.Title, shortcut.ShowInTray, shortcut.Icon, shortcut.Subtitle);
    }

    /// <summary>The last shortcut used and when, for the Shortcuts page.</summary>
    public (SpineShortcut Shortcut, DateTime At)? Last { get; private set; }

    public event Action? Invoked;

    public Task InvokeAsync(string shortcutId)
    {
        if (All.FirstOrDefault(s => s.Id == shortcutId) is { } shortcut)
        {
            Last = (shortcut, DateTime.Now);
            Invoked?.Invoke();
        }

        return shortcutId switch
        {
            "live-score" => _navigation.ShowAsync<LiveActivitiesPage>(),
            "widgets" => _navigation.ShowAsync<WidgetsPage>(),
            "scan" => _navigation.ShowAsync<BarcodesPage, OpenScanner>(new OpenScanner()),
            "theme" => _navigation.ShowAsync<ThemePage>(),
            _ => Task.CompletedTask,
        };
    }
}
