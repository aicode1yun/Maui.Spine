using Plugin.Maui.Spine.Svg.Icons;

namespace MauiSpineSampleApp.Pages.SvgIcons;

public partial class SvgIconsPageViewModel(INavigationService _navigation) : SampleViewModel
{
    public IReadOnlyList<string> Preview { get; } =
    [
        SpineIcons.Bell, SpineIcons.Search, SpineIcons.Share, SpineIcons.Heart, SpineIcons.Filter, SpineIcons.Calendar,
        SpineIcons.Lock, SpineIcons.Map, SpineIcons.Lamp, SpineIcons.Speaker, SpineIcons.Play, SpineIcons.Weather2,
    ];

    public string BrowseText => $"Browse all {SpineIcons.All.Count}";

    [RelayCommand]
    private Task Browse() => _navigation.NavigateToAsync<SvgIconSetPage>();
}
