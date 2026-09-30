namespace MauiSpineSampleApp.Pages.Glass;

public partial class GlassPageViewModel : SampleViewModel
{
    private readonly ChoiceGroup _background;

    public GlassPageViewModel()
    {
        _background = new("Behind the buttons",
        [
            new("Photo", "Glass bends and tints what is behind it, so over a photo each style shows its character.", () => PickBackground(true)),
            new("Plain page", "With nothing to refract, Clear all but disappears: keep it for photos and maps.", () => PickBackground(false)),
        ]);
        _background.Select(0);
    }

    [ObservableProperty]
    public partial bool ShowPhoto { get; set; } = true;

    [ObservableProperty]
    public partial bool ButtonsEnabled { get; set; } = true;

    [RelayCommand]
    private Task ShowStyleOptions() => ShowOptionsAsync("Glass styles",
        _background,
        new ToggleOption("Enabled", "A disabled glass button dims like any other.", () => ButtonsEnabled, value => ButtonsEnabled = value));

    private void PickBackground(bool photo)
    {
        ShowPhoto = photo;
        _background.Select(photo ? 0 : 1);
    }
}
