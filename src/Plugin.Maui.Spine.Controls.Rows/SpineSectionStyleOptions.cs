using Plugin.Maui.Spine.Core;

namespace Plugin.Maui.Spine.Controls;

/// <summary>
/// Look of a <see cref="SpineSection"/>. Set on one section through
/// <see cref="SpineSection.StyleOptions"/>, or app-wide as a resource keyed
/// <c>DefaultSpineSectionStyleOptions</c>; see <see cref="SpineStyleOptions{TSelf}"/> for the chain.
/// </summary>
/// <remarks>
/// The defaults follow each platform's settings screen. iOS: a filled group with continuous
/// 26-point corners (10 before iOS 26), hairline separators from the text to 16 points before the
/// edge, a bold 17-point grey header (13 points before iOS 26) and a 13-point footer; the page behind should be
/// <see cref="SpineSection.PageBackgroundLight"/> / <see cref="SpineSection.PageBackgroundDark"/>.
/// Android: no group and no separators, a header in the accent colour, as in Android's settings.
/// Windows: a card with hairline separators.
/// </remarks>
public class SpineSectionStyleOptions : SpineStyleOptions<SpineSectionStyleOptions>
{
    static readonly bool IsAndroid = DeviceInfo.Platform == DevicePlatform.Android;
    static readonly bool IsWindows = DeviceInfo.Platform == DevicePlatform.WinUI;
    static readonly bool IsModernApple = OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26);

    /// <summary>Font of the header and the footer. <see langword="null"/> = the app's default font.</summary>
    public string? FontFamily { get; set; }

    // iOS 26 Settings: the header as large as a row's title, bold, in the secondary label colour.
    public double HeaderFontSize { get; set; } = IsAndroid ? 14 : IsModernApple ? 17 : 13;
    public FontAttributes HeaderFontAttributes { get; set; } = IsAndroid || IsModernApple ? FontAttributes.Bold : FontAttributes.None;
    public double FooterFontSize { get; set; } = IsAndroid ? 14 : 13;

    /// <summary>Corner radius of the group.</summary>
    public double CornerRadius { get; set; } = IsAndroid ? 0 : IsWindows ? 8 : IsModernApple ? 26 : 10;

    /// <summary>Whether separators are drawn between the rows. Android's lists use none.</summary>
    public bool ShowSeparators { get; set; } = !IsAndroid;

    /// <summary>Space between a separator's end and the group's trailing edge.</summary>
    public double SeparatorTrailingInset { get; set; } = 16;

    /// <summary>Space before the header and footer text, from the group's edge.</summary>
    public double TextInset { get; set; } = 16;

    /// <summary>Fill of the group. <see cref="Colors.Transparent"/> on Android: the rows sit on the page.</summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>Colour of the separators. Keep it opaque: an alpha colour on a BoxView paints over black on iOS.</summary>
    public Color? SeparatorColor { get; set; }

    public Color? HeaderColor { get; set; }
    public Color? FooterColor { get; set; }

    protected override void InheritColorsFrom(SpineSectionStyleOptions source)
    {
        BackgroundColor ??= source.BackgroundColor;
        SeparatorColor ??= source.SeparatorColor;
        HeaderColor ??= source.HeaderColor;
        FooterColor ??= source.FooterColor;
    }

    protected override void ApplyThemeDefaults(AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark;
        var secondary = Color.FromArgb(dark ? "#8D8D93" : "#6D6D72");

        // secondarySystemGroupedBackground and opaqueSeparator on Apple; a card on Windows.
        BackgroundColor ??= IsAndroid ? Colors.Transparent
            : IsWindows ? Color.FromArgb(dark ? "#2B2B2B" : "#FFFFFF")
            : Color.FromArgb(dark ? "#1C1C1E" : "#FFFFFF");
        SeparatorColor ??= Color.FromArgb(dark ? "#38383A" : "#C6C6C8");
        HeaderColor ??= IsAndroid ? SpineTheme.GetAccent(theme) ?? secondary : secondary;
        FooterColor ??= secondary;
    }
}
