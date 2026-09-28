using Plugin.Maui.Spine.Svg.Icons;

namespace MauiSpineSampleApp.Pages.SvgIcons;

public partial class SvgIconSetPageViewModel : SampleViewModel
{
    public IReadOnlyList<string> Icons => SpineIcons.All;
}
