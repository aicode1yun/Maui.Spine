using System.ComponentModel;

namespace MauiSpineSampleApp.Pages.ScrollInset;

public partial class ScrollInsetPageViewModel : SampleViewModel
{
    public IReadOnlyList<string> Rows { get; } = [.. Enumerable.Range(1, 40).Select(i => i == 40 ? "Row 40, the last" : $"Row {i}")];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Edges), nameof(Status))]
    public partial bool IsInsetEnabled { get; set; } = true;

    // Top is here because a header bar the rows show through adds Top to the list's inset itself,
    // and setting the value would replace this binding; Top is zero while the bar is solid.
    public Plugin.Maui.Spine.Core.SafeAreaEdges Edges => IsInsetEnabled
        ? Plugin.Maui.Spine.Core.SafeAreaEdges.Top | Plugin.Maui.Spine.Core.SafeAreaEdges.Bottom
        : Plugin.Maui.Spine.Core.SafeAreaEdges.Top;

    public string Status => IsInsetEnabled
        ? "Inset on: the last row stops above the red band."
        : "Inset off: the red band covers the last row.";

    public double BottomInset => SafeAreaInsets.Bottom;

    [RelayCommand]
    private Task ShowInsetOptions() => ShowOptionsAsync("Scroll inset",
        new ToggleOption("Scroll inset", "Adds the home indicator's height to the end of the list, so the last row can scroll clear of it.", () => IsInsetEnabled, value => IsInsetEnabled = value));

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(SafeAreaInsets))
            OnPropertyChanged(nameof(BottomInset));
    }
}
