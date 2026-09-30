using Plugin.Maui.Spine.Core;
using Xunit;

namespace Plugin.Maui.Spine.Core.Tests;

/// <summary>
/// The names a shortcut icon goes by. <see cref="ShortcutIcons.ImageName"/> must be the name the build
/// renders the SVG under (build/Plugin.Maui.Spine.targets), and a valid Android resource name.
/// </summary>
public class ShortcutIconsTests
{
    [Theory]
    [InlineData("qrcode", "spine_shortcut_qrcode")]
    [InlineData("QrCode.svg", "spine_shortcut_qrcode")]
    [InlineData("figure.run", "spine_shortcut_figure_run")]
    [InlineData("figure_run.SVG", "spine_shortcut_figure_run")]
    [InlineData("my-icon", "spine_shortcut_my_icon")]
    [InlineData("Weather1n", "spine_shortcut_weather1n")]
    public void Image_name_is_lower_case_with_underscores(string icon, string expected) =>
        Assert.Equal(expected, ShortcutIcons.ImageName(icon));

    [Theory]
    [InlineData("qrcode", "qrcode.svg")]
    [InlineData("figure.run", "figure_run.svg")]
    [InlineData("QrCode.svg", "QrCode.svg")]
    public void Svg_file_reads_dots_as_underscores(string icon, string expected) =>
        Assert.Equal(expected, ShortcutIcons.SvgFile(icon));

    [Theory]
    [InlineData("star.fill", "star.fill")]
    [InlineData("figure_run", "figure.run")]
    [InlineData("bolt.svg", "bolt")]
    public void System_name_reads_underscores_as_dots(string icon, string expected) =>
        Assert.Equal(expected, ShortcutIcons.SystemName(icon));
}
