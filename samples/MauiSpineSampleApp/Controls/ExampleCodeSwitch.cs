using MauiSpineSampleApp.Pages;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSpineSampleApp.Controls;

/// <summary>
/// The two-segment switch at the top of a sample page, bound to <see cref="SampleViewModel.ShowCode"/>:
/// the page shows its examples, or the code for the same examples.
/// </summary>
public sealed class ExampleCodeSwitch : ContentView
{
    private readonly Border _track = new() { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 9 }, Padding = 2 };
    private readonly Segment _example;
    private readonly Segment _code;

    public ExampleCodeSwitch()
    {
        _example = new Segment("Example", () => Select(false));
        _code = new Segment("Code", () => Select(true));

        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], HeightRequest = 32 };
        grid.Add(_example.View);
        grid.Add(_code.View, 1);
        _track.Content = grid;
        Content = _track;

        Paint();
        SpineTheme.Track(this, Paint);
    }

    private SampleViewModel? _page;

    private bool ShowCode => _page?.ShowCode ?? false;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_page is not null)
            _page.PropertyChanged -= OnPagePropertyChanged;

        _page = BindingContext as SampleViewModel;

        if (_page is not null)
            _page.PropertyChanged += OnPagePropertyChanged;

        Paint();
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SampleViewModel.ShowCode))
            Paint();
    }

    private void Select(bool showCode)
    {
        if (_page is not null)
            _page.ShowCode = showCode;
    }

    // The platform's segmented control: a grey track and a raised thumb under the selected segment.
    private void Paint()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;

        _track.BackgroundColor = Color.FromArgb("#767680").WithAlpha(dark ? 0.24f : 0.12f);
        _example.Paint(!ShowCode, dark);
        _code.Paint(ShowCode, dark);
    }

    private sealed class Segment
    {
        private readonly Border _thumb = new() { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 7 } };
        private readonly Label _label;

        public Segment(string text, Action select)
        {
            _label = new Label { Text = text, FontSize = 14, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            _thumb.Content = _label;
            _thumb.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(select) });
            SemanticProperties.SetDescription(_thumb, text);
        }

        public View View => _thumb;

        public void Paint(bool selected, bool dark)
        {
            _thumb.BackgroundColor = selected ? (dark ? Color.FromArgb("#636366") : Colors.White) : Colors.Transparent;
            _label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
            _label.TextColor = dark ? Colors.White : Colors.Black;
            SemanticProperties.SetHint(_thumb, selected ? "Selected" : null);
        }
    }
}
