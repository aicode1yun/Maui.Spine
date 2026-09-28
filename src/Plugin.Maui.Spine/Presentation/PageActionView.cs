using Plugin.Maui.Spine.Core;
using Plugin.Maui.Spine.Extensions;
using Plugin.Maui.Spine.Svg;

namespace Plugin.Maui.Spine.Presentation;

/// <summary>
/// One header bar action. It draws the action on one of two faces: when the action is replaced by
/// another (Back by Cancel, Filter by Bell) the new face fades and grows in while the old one fades
/// and shrinks out, at the same time, as a navigation bar's items do.
/// </summary>
/// <remarks>
/// With Liquid Glass and <see cref="SpineOptions.ApplePlatformOptions.MorphHeaderActions"/> there is one
/// glass button for icons and text alike, and a replacement changes it inside a UIKit spring
/// animation, so the glass itself morphs from a circle to a capsule.
/// </remarks>
internal sealed class PageActionView : ContentView
{
    public static readonly BindableProperty ActionProperty = BindableProperty.Create(
        nameof(Action),
        typeof(PageAction),
        typeof(PageActionView),
        default(PageAction),
        propertyChanged: OnActionChanged);

    /// <summary>A fixed colour for the text and the icon, or <see langword="null"/> to follow the theme.</summary>
    public static readonly BindableProperty ForegroundProperty = BindableProperty.Create(
        nameof(Foreground), typeof(Color), typeof(PageActionView), null,
        propertyChanged: static (b, _, _) => ((PageActionView)b).ForEachFace(f => f.ApplyForeground()));

    public Color? Foreground
    {
        get => (Color?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly BindableProperty HideDisabledProperty = BindableProperty.Create(
        nameof(HideDisabled),
        typeof(bool),
        typeof(PageActionView),
        false,
        propertyChanged: static (b, _, _) => ((PageActionView)b).ForEachFace(f => f.ApplyHideDisabled()));

    /// <summary>
    /// Width of the slot an icon action takes. A text action sizes to its text; the view itself
    /// always sizes to its content, so a face that is fading out keeps its own size.
    /// </summary>
    public static readonly BindableProperty IconWidthProperty = BindableProperty.Create(
        nameof(IconWidth), typeof(double), typeof(PageActionView), HeaderBarConstants.Height,
        propertyChanged: static (b, _, _) => ((PageActionView)b)._front.ApplyIconWidth());

    public PageAction? Action
    {
        get => (PageAction?)GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public bool HideDisabled
    {
        get => (bool)GetValue(HideDisabledProperty);
        set => SetValue(HideDisabledProperty, value);
    }

    public double IconWidth
    {
        get => (double)GetValue(IconWidthProperty);
        set => SetValue(IconWidthProperty, value);
    }

    readonly bool _glass;
    readonly bool _morph;
    Face _front;
    Face _back;

    public PageActionView()
    {
        _glass = UseGlassHeaderActions;
        _morph = _glass && OperatingSystem.IsIOSVersionAtLeast(26)
            && IPlatformApplication.Current?.Services.GetService<SpineOptions>()?.Apple.MorphHeaderActions == true;
        _front = new Face(this);
        _back = new Face(this) { Opacity = 0, IsVisible = false, InputTransparent = true };

        Content = new Grid { Children = { _back, _front } };

        // Keep the colours in sync when the user switches the theme or the accent at runtime.
        SpineTheme.Track(this, () => ForEachFace(f => f.ApplyForeground()));

        _front.Apply(null);
    }

    // The option is read here rather than passed down: HeaderBarView and PageActionView are built by
    // pages, not by DI, and the attached property is a no-op off Apple anyway.
    internal static bool UseGlassHeaderActions =>
        OperatingSystem.IsIOS()
        && IPlatformApplication.Current?.Services.GetService<SpineOptions>()?.Apple.GlassHeaderActions == true;

    /// <summary>Raised when the assigned action's <see cref="PageAction.IsVisible"/> changes in place.</summary>
    public event Action? VisibilityChanged;

    /// <summary>
    /// How long a header bar action takes to swap, show or hide: close to UIKit's navigation bar on
    /// Apple, Material 3's fade-through on Android. Short and without movement under Reduce Motion.
    /// </summary>
    internal static uint TransitionDuration => ReducedMotion.IsOn ? 150u
        : DeviceInfo.Platform == DevicePlatform.Android ? 150u
        : DeviceInfo.Platform == DevicePlatform.WinUI ? 200u
        : 300u;

    /// <summary>The scale an action grows from and shrinks to; 1 (none) under Reduce Motion.</summary>
    internal static double TransitionScale => ReducedMotion.IsOn ? 1 : 0.85;

    internal static readonly Easing TransitionEasing = Easing.CubicOut;

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == HeightRequestProperty.PropertyName && _front is not null)
            _front.ApplyIconWidth();
    }

    void ForEachFace(Action<Face> apply)
    {
        apply(_front);
        apply(_back);
    }

    static void OnActionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (PageActionView)bindable;

        if (oldValue is PageAction oldAction)
            oldAction.PropertyChanged -= view.OnActionPropertyChanged;
        if (newValue is PageAction newAction)
            newAction.PropertyChanged += view.OnActionPropertyChanged;

        var oldSvg = (oldValue as PageAction)?.Svg;
        var newSvg = (newValue as PageAction)?.Svg;
        var sameGlyph = !string.IsNullOrWhiteSpace(oldSvg) && oldSvg == newSvg;

        // The same icon under another action (a page's Save after the previous page's Save) is not
        // a change the user sees; anything else crosses over.
        if (sameGlyph || view.Opacity == 0 || !view.IsVisible)
            view._front.Apply((PageAction?)newValue);
        else if (view._morph)
            view.Morph((PageAction?)newValue);
        else
            _ = view.SwapAsync((PageAction?)newValue);
    }

    void OnActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PageAction.Svg) when _morph:
                Morph(Action);
                break;
            case nameof(PageAction.Svg):
                _ = SwapAsync(Action);
                break;
            case nameof(PageAction.IsVisible):
                _front.Apply(Action);
                VisibilityChanged?.Invoke();
                break;
            default:
                _front.Apply(Action);
                break;
        }
    }

    /// <summary>
    /// Changes the one glass button to <paramref name="action"/> and lays the bar out again inside a
    /// spring animation: UIKit animates the new frame and the button's content, and the glass
    /// reshapes with it.
    /// </summary>
    void Morph(PageAction? action)
    {
#if IOS || MACCATALYST
        if (Window?.Handler?.PlatformView is UIKit.UIWindow window)
        {
            var reduced = ReducedMotion.IsOn;
            var face = _front;
            var version = ++_morphVersion;

            // The old title or icon fades first; revealing the new one while the glass grows would
            // show it clipped by the capsule.
            _ = GlassAppearance.FadeContentAsync(face, show: false, 90).ContinueWith(_ =>
            {
                if (version != _morphVersion)
                    return;

                // The new content goes in at once and invisible; the new size is laid out inside
                // the spring below, so the glass grows or shrinks to it.
                UIKit.UIView.PerformWithoutAnimation(() => face.Apply(action));
                GlassAppearance.SetContentAlpha(face, 0);
                ((IView)this).InvalidateMeasure();

                UIKit.UIView.AnimateNotify(
                    reduced ? 0.2 : 0.45, 0, reduced ? 1f : 0.82f, 0,
                    UIKit.UIViewAnimationOptions.BeginFromCurrentState | UIKit.UIViewAnimationOptions.AllowUserInteraction,
                    window.LayoutIfNeeded,
                    null);

                _ = GlassAppearance.FadeContentAsync(face, show: true, reduced ? 120u : 220u, delay: reduced ? 0 : 0.12);
            }, TaskScheduler.FromCurrentSynchronizationContext());
            return;
        }
#endif
        _front.Apply(action);
    }

    int _morphVersion;

    /// <summary>Crosses from what the front face shows to <paramref name="action"/> on the other face.</summary>
    async Task SwapAsync(PageAction? action)
    {
        var outgoing = _front;
        var incoming = _back;

        this.AbortAnimation("Swap");

        _front = incoming;
        _back = outgoing;

        // The outgoing face keeps the size it has now, whatever the slot does next.
        outgoing.Freeze();
        outgoing.InputTransparent = true;

        incoming.Apply(action);
        incoming.InputTransparent = false;
        incoming.IsVisible = true;
        incoming.Opacity = 0;
        incoming.Scale = TransitionScale;

        // Drawn above the outgoing face, so a tap during the swap reaches the new action.
        incoming.ZIndex = 1;
        outgoing.ZIndex = 0;

        var duration = TransitionDuration;
        var shrink = TransitionScale;

#if IOS || MACCATALYST
        // Glass turns flat grey when its alpha fades: materialize and dissolve it instead.
        if (_glass && GlassAppearance.Applies(this))
        {
            incoming.Opacity = 1;
            outgoing.Opacity = 1;

            await Task.WhenAll(
                GlassAppearance.AnimateAsync(incoming, show: true, duration),
                incoming.ScaleToAsync(1, duration, TransitionEasing),
                GlassAppearance.AnimateAsync(outgoing, show: false, duration * 7 / 10),
                outgoing.ScaleToAsync(shrink, duration * 7 / 10, TransitionEasing));

            if (ReferenceEquals(_back, outgoing))
            {
                outgoing.IsVisible = false;
                outgoing.Scale = 1;
                outgoing.Apply(null);
            }
            return;
        }
#endif

        var tcs = new TaskCompletionSource();

        new Animation
        {
            { 0, 1, new Animation(v => incoming.Opacity = v, 0, 1) },
            { 0, 1, new Animation(v => incoming.Scale = v, shrink, 1) },
            // The old face is gone a little before the new one has settled, as UIKit's crossfade.
            { 0, 0.7, new Animation(v => outgoing.Opacity = v, outgoing.Opacity, 0) },
            { 0, 0.7, new Animation(v => outgoing.Scale = v, 1, shrink) },
        }.Commit(this, "Swap", 16, duration, TransitionEasing, (_, cancelled) =>
        {
            if (!cancelled && ReferenceEquals(_back, outgoing))
            {
                outgoing.IsVisible = false;
                outgoing.Scale = 1;
                outgoing.Apply(null);
            }

            incoming.Opacity = 1;
            incoming.Scale = 1;
            tcs.TrySetResult();
        });

        await tcs.Task;
    }

    /// <summary>One way of drawing the action: a text button, an icon button and a badge.</summary>
    sealed class Face : Grid
    {
        readonly PageActionView _owner;
        readonly Button _textButton;
        readonly ImageButton _imageButton;
        readonly Border _badge;
        readonly Label _badgeLabel;
        string? _currentSvg;

        public Face(PageActionView owner)
        {
            _owner = owner;

            // Primary actions sit at the leading edge and secondary ones at the trailing edge; two
            // faces of different widths overlap at that edge.
            SetBinding(HorizontalOptionsProperty, new Binding(nameof(HorizontalOptions), source: owner));

            _textButton = new Button
            {
                BackgroundColor = Colors.Transparent,
                BorderWidth = 0,
                Margin = new Thickness(12, 0, 12, 0),
            };

            ButtonExtensions.SetCompact(_textButton, true);

            // Re-apply in HandlerChanged because the implicit Button style is applied when the
            // view enters the visual tree and can race with the initial assignment.
            // Direct SetValue (not a binding) definitively wins over any style setter.
            _textButton.HandlerChanged += (_, _) =>
            {
                if (_textButton.Handler is not null)
                    ApplyForeground();
            };

            _imageButton = new ImageButton
            {
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                BackgroundColor = Colors.Transparent,
                BorderWidth = 0,
                BorderColor = Colors.Transparent,
                CornerRadius = DeviceInfo.Platform == DevicePlatform.Android ? 24 : 0,
            };
            _imageButton.ApplyCommonVisualStates(owner.HideDisabled);

            _imageButton.SetBinding(VisualElement.HeightRequestProperty, new Binding(nameof(HeightRequest), source: owner));
            _imageButton.SetBinding(ImageButton.PaddingProperty, new Binding(nameof(Padding), source: owner));
            ApplyIconWidth();

            if (owner._glass)
            {
                // Compact zeroed the padding; the capsule needs some room around the text, and it
                // sits centred in the 44-point row rather than filling it. The capsule is the
                // button's edge, so it lines up with the page margin like an icon's circle; the
                // plain text button's inset would push it 12 points further in.
                _textButton.Margin = new Thickness(0);
                _textButton.Padding = new Thickness(14, 8);
                _textButton.VerticalOptions = LayoutOptions.Center;
                Glass.SetStyle(_textButton, GlassStyle.Regular);

                // The glass makes the slot visible, so the icon becomes a circle centred in it
                // rather than a pill hugging the screen edge.
                _imageButton.HorizontalOptions = LayoutOptions.Center;
                Glass.SetStyle(_imageButton, GlassStyle.Regular);
            }

            _badgeLabel = new Label
            {
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.NoWrap,
            };

            // The same red the native tab badges use, so a count reads the same everywhere.
            _badge = new Border
            {
                Content = _badgeLabel,
                BackgroundColor = Color.FromArgb("#FF3B30"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(4, 0),
                MinimumWidthRequest = 16,
                HeightRequest = 16,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                // On the glass capsule the pill sits inside the slot's corner; on a plain text button
                // it sits past the text, which ends 12 points short of the edge.
                Margin = owner._glass ? new Thickness(0, 2, 6, 0) : new Thickness(0, 0, 0, 0),
                InputTransparent = true,
                IsVisible = false,
            };

            Children.Add(_textButton);
            Children.Add(_imageButton);
            Children.Add(_badge);

            ApplyForeground();
        }

        /// <summary>
        /// The one glass button draws icons too: an icon action is a circle as tall as the row with
        /// the SVG as its image, a text action a capsule around its text.
        /// </summary>
        void ApplyMorphing(PageAction action, bool hasSvg)
        {
            _imageButton.IsVisible = false;
            _textButton.IsVisible = true;
            _textButton.IsEnabled = action.IsEnabled;
            _textButton.Opacity = action.IsEnabled ? 1 : 0.4;

            _badgeLabel.Text = action.Badge ?? string.Empty;
            _badge.IsVisible = !string.IsNullOrEmpty(action.Badge);
            SemanticProperties.SetDescription(_textButton, action.Description ?? (hasSvg ? null : action.Text));
            MenuButton.SetItems(_textButton, action.Menu);
            MenuButton.SetShowsSelection(_textButton, action.MenuShowsSelection);

            _currentSvg = hasSvg ? action.Svg : null;

            if (hasSvg)
            {
                _textButton.Text = string.Empty;
                _textButton.Padding = new Thickness(0);
                _textButton.WidthRequest = _owner.HeightRequest;
                _textButton.HeightRequest = _owner.HeightRequest;
                ApplyMorphingImage();
            }
            else
            {
                _textButton.ImageSource = null;
                _textButton.Padding = new Thickness(14, 8);
                _textButton.WidthRequest = -1;
                _textButton.HeightRequest = -1;
                _textButton.Text = action.Text ?? string.Empty;
            }

            _textButton.Command = action.Command;
            _textButton.CommandParameter = action.CommandParameter;
        }

        // A 24-point glyph, the size a UIBarButtonItem uses, tinted like the icon buttons.
        void ApplyMorphingImage()
        {
            var names = IPlatformApplication.Current?.Services.GetService<ResourceNameCache>();
            if (_currentSvg is not { } svg || names?.Resolve(svg) is not { } resource)
                return;

            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var tint = _owner.Foreground ?? (dark ? Colors.White : Colors.Black);
            _textButton.ImageSource = SvgBitmapLoader.LoadFromEmbedded(resource, 24, 24, tint);
        }

        /// <summary>A glass icon is a circle as tall as the row; otherwise it fills the slot the bar gives it.</summary>
        public void ApplyIconWidth()
        {
            _imageButton.RemoveBinding(VisualElement.WidthRequestProperty);
            _imageButton.WidthRequest = _owner._glass ? _owner.HeightRequest : _owner.IconWidth;
        }

        /// <summary>Holds the face at its current size while it fades out.</summary>
        public void Freeze()
        {
            if (Width > 0)
                WidthRequest = Width;
        }

        public void ApplyHideDisabled() => _imageButton.ApplyCommonVisualStates(_owner.HideDisabled);

        public void ApplyForeground()
        {
            if (_owner.Foreground is { } foreground)
            {
                _textButton.TextColor = foreground;
            }
            else
            {
                var isDark = Application.Current?.RequestedTheme == AppTheme.Dark
                    || (Application.Current?.RequestedTheme != AppTheme.Light
                        && Application.Current?.PlatformAppTheme == AppTheme.Dark);
                _textButton.TextColor = SpineTheme.GetAccent(isDark ? AppTheme.Dark : AppTheme.Light)
                    ?? Color.FromArgb(isDark ? "#0A84FF" : "#007AFF");
            }

            if (_imageButton.Behaviors.OfType<SvgImageSourceBehavior>().FirstOrDefault() is { } svg)
            {
                svg.TintColor = _owner.Foreground;
                svg.UpdateImage();
            }

            if (_owner._morph && _textButton.ImageSource is not null)
                ApplyMorphingImage();
        }

        public void Apply(PageAction? action)
        {
            WidthRequest = -1;
            ApplyIconWidth();

            if (action is null || !action.IsVisible)
            {
                _textButton.IsVisible = false;
                _imageButton.IsVisible = false;
                _badge.IsVisible = false;
                return;
            }

            var hasSvg = !string.IsNullOrWhiteSpace(action.Svg);

            if (_owner._morph)
            {
                ApplyMorphing(action, hasSvg);
                return;
            }

            _imageButton.IsVisible = hasSvg;
            _textButton.IsVisible = !hasSvg;
            _imageButton.IsEnabled = action.IsEnabled;
            _textButton.IsEnabled = action.IsEnabled;
            // The app's Disabled visual state may not reach a button whose colour is set directly.
            _imageButton.Opacity = action.IsEnabled ? 1 : 0.4;
            _textButton.Opacity = action.IsEnabled ? 1 : 0.4;

            _badgeLabel.Text = action.Badge ?? string.Empty;
            SemanticProperties.SetDescription(_imageButton, action.Description);
            SemanticProperties.SetDescription(_textButton, action.Description);

            MenuButton.SetItems(_imageButton, action.Menu);
            MenuButton.SetItems(_textButton, action.Menu);
            MenuButton.SetShowsSelection(_textButton, action.MenuShowsSelection);
            _badge.IsVisible = !string.IsNullOrEmpty(action.Badge);

            if (hasSvg)
            {
                if (action.Svg != _currentSvg)
                {
                    _imageButton.Behaviors.Clear();
                    var behavior = new SvgImageSourceBehavior
                    {
                        Svg = action.Svg!,
                        LightTintColor = Colors.Black,
                        DarkTintColor = Colors.White,
                        TintColor = _owner.Foreground,
                    };
                    // A 24-point glyph in the 44-point glass circle, the size a UIBarButtonItem uses.
                    if (_owner._glass)
                        behavior.Padding = new Thickness(10);
                    _imageButton.Behaviors.Add(behavior);
                    _currentSvg = action.Svg;
                }

                _imageButton.Command = action.Command;
                _imageButton.CommandParameter = action.CommandParameter;
            }
            else
            {
                if (_currentSvg is not null)
                {
                    _imageButton.Behaviors.Clear();
                    _currentSvg = null;
                }

                _textButton.Text = action.Text ?? string.Empty;
                _textButton.Command = action.Command;
                _textButton.CommandParameter = action.CommandParameter;
            }
        }
    }
}
