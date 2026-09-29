using MauiSpineSampleApp.Pages.Theme;

namespace MauiBottomSheetPoc;

public class ShortcutHandler(INavigationService _navigation) : IShortcutHandler
{
    public static void Configure(IShortcutBuilder builder)
    {
        builder.Add(id: "theme", title: "Theme", icon: "theme", subtitle: "Light, dark or the system");
    }

    public Task InvokeAsync(string shortcutId) =>
        shortcutId switch
        {
            "theme" => _navigation.NavigateToAsync<ThemePage>(),
            _ => Task.CompletedTask
        };
}
