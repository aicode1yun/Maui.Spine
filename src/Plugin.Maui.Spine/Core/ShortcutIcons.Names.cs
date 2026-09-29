using System.Text.RegularExpressions;

namespace Plugin.Maui.Spine.Core;

// The names alone, free of MAUI, so the test project can compile them.
internal static partial class ShortcutIcons
{
    /// <summary>
    /// "qrcode", "QrCode.svg" and "figure.run" become "qrcode", "qrcode" and "figure_run".
    /// Must stay the same as the Key the _SpineAddShortcutIcons target in build/Plugin.Maui.Spine.targets computes.
    /// </summary>
    public static string Key(string icon) => NotAlphanumeric().Replace(WithoutExtension(icon).ToLowerInvariant(), "_");

    /// <summary>The name the build renders the icon under: a valid Android resource name.</summary>
    public static string ImageName(string icon) => $"spine_shortcut_{Key(icon)}";

    /// <summary>The embedded SVG, for the tray menus, which draw it at run time.</summary>
    public static string SvgFile(string icon) => $"{WithoutExtension(icon).Replace('.', '_')}.svg";

    /// <summary>The SF Symbol iOS falls back to, as <c>W.Icon</c> does: underscores read as dots.</summary>
    public static string SystemName(string icon) => WithoutExtension(icon).Replace('_', '.');

    private static string WithoutExtension(string icon) =>
        icon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? icon[..^4] : icon;

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NotAlphanumeric();
}
