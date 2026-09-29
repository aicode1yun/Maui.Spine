using Microsoft.Extensions.Logging;
using Plugin.Maui.Spine.Svg;
#if IOS
using Foundation;
using UIKit;
#endif

namespace Plugin.Maui.Spine.Core;

/// <summary>
/// Turns a shortcut's icon name into what each platform shows. The home screen draws the icon, not
/// the app, so it has to be an image in the app itself: <c>&lt;SpineShortcutIcon&gt;</c> has the build
/// render the SVG under <see cref="ImageName"/> (an image in the iOS bundle, a drawable on Android).
/// </summary>
internal static partial class ShortcutIcons
{
    /// <summary>
    /// The icon to hand MAUI's <c>AppAction</c>: the rendered image's name when the app carries it,
    /// otherwise <see langword="null"/>, so MAUI is never given a name that resolves to nothing
    /// (a resource id of 0 on Android).
    /// </summary>
    public static string? ForAppAction(SpineShortcut shortcut)
    {
        if (shortcut.Icon is not { } icon) return null;
        var name = ImageName(icon);
#if ANDROID
        var context = Android.App.Application.Context;
        return context.Resources?.GetIdentifier(name, "drawable", context.PackageName) is > 0 ? name : null;
#elif IOS
        return UIImage.FromBundle(name) is not null ? name : null;
#elif WINDOWS
        return name;
#else
        return null;
#endif
    }

    /// <summary>
    /// The shortcut's SVG for a tray menu (Mac, Windows), which draws it at run time, or
    /// <see langword="null"/> when it has no icon or the SVG is not embedded, which is logged.
    /// </summary>
    public static SvgIcon? ForTray(IServiceProvider services, SpineShortcut shortcut)
    {
        if (shortcut.Icon is not { } icon || services.GetService<ISvgIconService>() is not { } svg) return null;
        try
        {
            return svg.FromEmbeddedSvg(SvgFile(icon));
        }
        catch (FileNotFoundException e)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger("Plugin.Maui.Spine.Shortcuts")
                .LogWarning(e, "Shortcut \"{Id}\": no embedded {Svg} for the tray menu.", shortcut.Id, SvgFile(icon));
            return null;
        }
    }

    /// <summary>
    /// Runs after MAUI has handed the shortcuts to the platform: says which icons the app does not
    /// carry, and on iOS gives those an SF Symbol of the same name where there is one.
    /// </summary>
    public sealed class Initializer(IReadOnlyList<SpineShortcut> shortcuts) : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
        {
            var missing = shortcuts.Where(s => s.Icon is not null && ForAppAction(s) is null).ToList();
            if (missing.Count == 0) return;

            var logger = services.GetService<ILoggerFactory>()?.CreateLogger("Plugin.Maui.Spine.Shortcuts");
#if IOS
            var symbols = missing
                .Where(s => UIImage.GetSystemImage(SystemName(s.Icon!)) is not null)
                .ToDictionary(s => s.Id, s => SystemName(s.Icon!));

            if (symbols.Count > 0 && UIApplication.SharedApplication.ShortcutItems is { } items)
            {
                // MAUI keeps the shortcut's id in UserInfo["id"]; the item is copied with only the icon changed.
                UIApplication.SharedApplication.ShortcutItems = [.. items.Select(item =>
                    item.UserInfo?.TryGetValue((NSString)"id", out var id) == true && id?.ToString() is { } key && symbols.TryGetValue(key, out var symbol)
                        ? new UIApplicationShortcutItem(item.Type, item.LocalizedTitle, item.LocalizedSubtitle, UIApplicationShortcutIcon.FromSystemImageName(symbol), item.UserInfo)
                        : item)];
            }

            foreach (var shortcut in missing)
            {
                if (symbols.TryGetValue(shortcut.Id, out var symbol))
                    logger?.LogWarning("Shortcut \"{Id}\": no image {Image} in the app, showing the SF Symbol {Symbol}. Declare <SpineShortcutIcon Include=\"{Icon}\" /> to use the SVG.", shortcut.Id, ImageName(shortcut.Icon!), symbol, shortcut.Icon);
                else
                    logger?.LogWarning("Shortcut \"{Id}\": no image {Image} in the app and no SF Symbol {Symbol}; iOS shows its default dot. Declare <SpineShortcutIcon Include=\"{Icon}\" />.", shortcut.Id, ImageName(shortcut.Icon!), SystemName(shortcut.Icon!), shortcut.Icon);
            }
#elif ANDROID
            foreach (var shortcut in missing)
                logger?.LogWarning("Shortcut \"{Id}\": no drawable {Image} in the app; it shows without an icon. Declare <SpineShortcutIcon Include=\"{Icon}\" />.", shortcut.Id, ImageName(shortcut.Icon!), shortcut.Icon);
#endif
        }
    }
}
