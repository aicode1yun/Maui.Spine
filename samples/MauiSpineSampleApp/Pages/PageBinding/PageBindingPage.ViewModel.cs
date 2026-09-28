namespace MauiSpineSampleApp.Pages.PageBinding;

public sealed record Fruit(string Name, decimal PricePerKg);

public partial class PageBindingPageViewModel : SampleViewModel
{
    public IReadOnlyList<Fruit> Fruits { get; } =
    [
        new("Apple", 29), new("Banana", 24), new("Cherry", 89), new("Damson", 45), new("Fig", 120), new("Grape", 59),
    ];

    // Page-level state that every row reads: the rows have no ShowPrices of their own.
    [ObservableProperty]
    public partial bool ShowPrices { get; set; } = true;

    [ObservableProperty]
    public partial string Picked { get; set; } = "Nothing picked yet";

    // A page-level command that a row's button runs with the row as parameter.
    [RelayCommand]
    private void Pick(Fruit fruit) => Picked = $"You picked {fruit.Name}";

    [RelayCommand]
    private Task ShowOptions() => ShowOptionsAsync("Page binding",
        new ToggleOption("Show prices", "One setting on the page, read by every row with PageBinding.", () => ShowPrices, v => ShowPrices = v));
}
