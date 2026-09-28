using System.Globalization;
using Plugin.Maui.Spine.Common;

namespace MauiSpineSampleApp.Pages.Strings;

public partial class StringsPageViewModel : SampleViewModel
{
    private readonly ISpineStrings _strings;
    private readonly ChoiceGroup _language;

    public StringsPageViewModel(ISpineStrings strings)
    {
        _strings = strings;

        _language = new("Language",
        [
            new("English", "strings.xml, the neutral document: what any language without its own file gets.", () => PickLanguage("en")),
            new("Svenska", "strings.sv.xml. A key it lacks falls back to strings.xml.", () => PickLanguage("sv")),
        ]);

        WhileVisible(() => _strings.Changed += OnStringsChanged, () => _strings.Changed -= OnStringsChanged);
    }

    [ObservableProperty]
    public partial int Apples { get; set; } = 1;

    [ObservableProperty]
    public partial string UserName { get; set; } = "Jonatan";

    [ObservableProperty]
    public partial DateTime Now { get; set; } = DateTime.Now;

    public string FromCode => _strings.Get("Strings.Apples", Apples);

    public string Missing => _strings["Strings.NoSuchKey"];

    partial void OnApplesChanged(int value) => OnPropertyChanged(nameof(FromCode));

    [RelayCommand]
    private Task ShowLanguageOptions()
    {
        _language.Select(_strings.Culture.TwoLetterISOLanguageName == "sv" ? 1 : 0);
        return ShowOptionsAsync("Language", _language);
    }

    // The options sheet covers the page, which stops hearing Changed while it is hidden.
    private void PickLanguage(string language)
    {
        _strings.Culture = CultureInfo.GetCultureInfo(language);
        _language.Select(language == "sv" ? 1 : 0);
        OnPropertyChanged(nameof(FromCode));
    }

    private void OnStringsChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(FromCode));

    [RelayCommand] private void More() => Apples++;
    [RelayCommand] private void Fewer() => Apples = Math.Max(0, Apples - 1);
}
