using System.Windows.Input;
using MauiSpineSampleApp.Pages;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSpineSampleApp.Controls;

/// <summary>
/// One example on a sample page: a title, a line on what the feature solves, and the live demo.
/// When the page is switched to Code the demo gives way to <see cref="Code"/>. An example with
/// settings to try sets <see cref="OptionsCommand"/>, which puts a "Try options" link next to the title.
/// </summary>
/// <remarks>
/// <see cref="Code"/> may be written indented in XAML; the common indentation and the blank
/// lines around it are removed.
/// </remarks>
[ContentProperty(nameof(Demo))]
public sealed class Example : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(Example), null,
        propertyChanged: (b, _, n) => ((Example)b)._title.Text = (string?)n);

    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(
        nameof(Summary), typeof(string), typeof(Example), null,
        propertyChanged: (b, _, n) => ((Example)b)._summary.Text = (string?)n);

    public static readonly BindableProperty CodeProperty = BindableProperty.Create(
        nameof(Code), typeof(string), typeof(Example), null,
        propertyChanged: (b, _, n) => ((Example)b)._code.Text = Dedent((string?)n));

    public static readonly BindableProperty DemoProperty = BindableProperty.Create(
        nameof(Demo), typeof(View), typeof(Example), null,
        propertyChanged: (b, _, n) => ((Example)b)._demo.Content = (View?)n);

    public static readonly BindableProperty OptionsCommandProperty = BindableProperty.Create(
        nameof(OptionsCommand), typeof(ICommand), typeof(Example), null,
        propertyChanged: (b, _, _) => ((Example)b).Update());

    public static readonly BindableProperty ShowCodeProperty = BindableProperty.Create(
        nameof(ShowCode), typeof(bool), typeof(Example), false,
        propertyChanged: (b, _, _) => ((Example)b).Update());

    private readonly Label _title = new() { FontSize = 18, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
    private readonly Label _summary = new() { FontSize = 15 };
    private readonly ContentView _demo = new();
    private readonly Button _options = new()
    {
        Text = "Try options",
        FontSize = 14,
        Padding = new Thickness(12, 4),
        MinimumHeightRequest = 30,
        HeightRequest = 30,
        CornerRadius = 15,
        VerticalOptions = LayoutOptions.Center,
    };
    private readonly Label _code = new()
    {
        FontSize = 12,
        FontFamily = DeviceInfo.Platform == DevicePlatform.Android ? "monospace"
            : DeviceInfo.Platform == DevicePlatform.WinUI ? "Consolas"
            : "Menlo",
    };
    private readonly Border _codeBlock;

    public Example()
    {
        _options.Clicked += OnOptionsClicked;

        _codeBlock = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(12, 10),
            Content = new CodeScroller { Content = _code },
        };

        var titleRow = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        titleRow.Add(_title);
        titleRow.Add(_options, 1);

        Content = new VerticalStackLayout { Spacing = 10, Children = { titleRow, _summary, _demo, _codeBlock } };

        Update();
        Paint();
        SpineTheme.Track(this, Paint);
    }

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>What the feature does, adds or solves, in a sentence or two.</summary>
    public string? Summary { get => (string?)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <summary>The code behind the demo, shown instead of it when the page is switched to Code.</summary>
    public string? Code { get => (string?)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    public View? Demo { get => (View?)GetValue(DemoProperty); set => SetValue(DemoProperty, value); }

    /// <summary>Opens the example's settings, usually in a sheet; the link is hidden without one.</summary>
    public ICommand? OptionsCommand { get => (ICommand?)GetValue(OptionsCommandProperty); set => SetValue(OptionsCommandProperty, value); }

    public bool ShowCode { get => (bool)GetValue(ShowCodeProperty); set => SetValue(ShowCodeProperty, value); }

    private SampleViewModel? _page;

    // The page's switch: every example on a sample page follows it.
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_page is not null)
            _page.PropertyChanged -= OnPagePropertyChanged;

        _page = BindingContext as SampleViewModel;

        if (_page is not null)
        {
            _page.PropertyChanged += OnPagePropertyChanged;
            ShowCode = _page.ShowCode;
        }
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SampleViewModel.ShowCode))
            ShowCode = _page!.ShowCode;
    }

    // The options open in a sheet over the lower half of the screen: bring the example to the top first, so its demo stays in view.
    private async void OnOptionsClicked(object? sender, EventArgs e)
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ScrollView scrollView)
            {
                // An animated scroll that does not move never completes on iOS, which left the link dead once the
                // example was at the top or the page could scroll no further; wait only for a scroll that has moved
                var before = scrollView.ScrollY;
                var scroll = scrollView.ScrollToAsync(this, ScrollToPosition.Start, true);
                if (await Task.WhenAny(scroll, Task.Delay(100)) != scroll && scrollView.ScrollY != before)
                    await scroll;
                break;
            }
        }

        if (OptionsCommand?.CanExecute(null) == true)
            OptionsCommand.Execute(null);
    }

    private void Update()
    {
        _demo.IsVisible = !ShowCode;
        _codeBlock.IsVisible = ShowCode;
        _options.IsVisible = !ShowCode && OptionsCommand is not null;
    }

    private void Paint()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var accent = SpineTheme.GetAccent(dark ? AppTheme.Dark : AppTheme.Light) ?? Colors.Gray;

        _summary.TextColor = dark ? Color.FromArgb("#EBEBF5").WithAlpha(0.6f) : Color.FromArgb("#3C3C43").WithAlpha(0.6f);
        _options.BackgroundColor = accent.WithAlpha(dark ? 0.25f : 0.13f);
        _options.TextColor = accent;
        _codeBlock.BackgroundColor = Color.FromArgb("#808080").WithAlpha(dark ? 0.2f : 0.1f);
    }

    private static string? Dedent(string? code)
    {
        if (code is null)
            return null;

        var lines = code.Replace("\r\n", "\n").Split('\n').ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        // XAML trims the first line of element text, so only the lines after it show the indentation.
        var indent = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();

        return string.Join('\n', lines.Select((l, i) => i == 0 ? l.Trim() : l.Length >= indent ? l[indent..].TrimEnd() : l.TrimEnd()));
    }

    /// <summary>
    /// Scrolls long lines sideways. It measures the code at any width, so lines do not wrap (a
    /// NoWrap label would be a single line on iOS), and asks for no more than the width it is
    /// offered, or the whole page would grow to the longest line.
    /// </summary>
    private sealed class CodeScroller : ScrollView
    {
        public CodeScroller()
        {
            Orientation = ScrollOrientation.Horizontal;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never;
        }

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            var size = base.MeasureOverride(widthConstraint, heightConstraint);
            return new Size(Math.Min(size.Width, widthConstraint), size.Height);
        }
    }
}
