namespace MauiSpineSampleApp.Controls;

/// <summary>
/// The body of a sample page: the Example / Code switch and the packages at the top, then the
/// page's <see cref="Example"/>s. It is the page's scroll view, so the page's scroll inset reaches
/// it and the last example scrolls clear of the home indicator.
/// </summary>
[ContentProperty(nameof(Examples))]
public sealed class SampleLayout : ScrollView
{
    public static readonly BindableProperty PackagesProperty = BindableProperty.Create(
        nameof(Packages), typeof(string), typeof(SampleLayout), null,
        propertyChanged: (b, _, n) => ((SampleLayout)b)._packages.Packages = (string?)n);

    private readonly VerticalStackLayout _stack = new() { Padding = new Thickness(HeaderBarConstants.PageMargin, 8, HeaderBarConstants.PageMargin, 24), Spacing = 36 };
    private readonly PackageChips _packages = new();

    public SampleLayout()
    {
        _stack.Children.Add(new VerticalStackLayout { Spacing = 12, Children = { new ExampleCodeSwitch(), _packages } });
        Content = _stack;
    }

    /// <summary>Comma-separated NuGet ids the page's examples need.</summary>
    public string? Packages { get => (string?)GetValue(PackagesProperty); set => SetValue(PackagesProperty, value); }

    public IList<IView> Examples => _stack.Children;
}
