using Plugin.Maui.Spine.Extensions;

namespace Plugin.Maui.Spine.Presentation;

/// <summary>
/// The default <see cref="ISpineTransitions"/> implementation.
/// Moves pages as iOS's navigation does on every platform: the upper page slides over the lower one,
/// which moves a quarter of the way aside and dims. Motion runs on the platform's own animation
/// engine where there is one (<see cref="SpineAnimation"/>).
/// Register a custom <see cref="ISpineTransitions"/> in DI after calling <c>UseSpine</c> to override these defaults.
/// </summary>
public class DefaultSpineTransitions : ISpineTransitions
{
    /// <summary>Animation duration in milliseconds, tuned per platform.</summary>
    protected virtual uint Duration => GetPlatformDuration();

    /// <summary>Easing function applied to all transitions.</summary>
    protected virtual Easing Ease => Easing.CubicOut;

    private static uint GetPlatformDuration()
    {
        // Align transition timing with typical platform navigation durations
        return DeviceInfo.Platform switch
        {
            var p when p == DevicePlatform.iOS || p == DevicePlatform.MacCatalyst => 300,
            var p when p == DevicePlatform.Android => 300,
            var p when p == DevicePlatform.WinUI => 250,
            _ => 300
        };
    }

    /// <summary>Returns the width of the given view, falling back to the current window width.</summary>
    protected static double GetWidth(View view)
    {
        if (view?.Width > 0)
            return view.Width;

        return Application.Current?.Windows[0]?.Page?.Width ?? 400;
    }

    /// <inheritdoc/>
    public virtual Task AnimateSetRootAsync(View view)
    {
        view.Opacity = 1;
        view.IsVisible = true;
        return Task.CompletedTask;
    }

    /// <summary>How far the lower page moves aside while it is covered: a quarter of the width, as iOS's navigation does.</summary>
    protected virtual double Parallax => 0.25;

    /// <summary>The opacity of the dim over a covered page.</summary>
    protected virtual double DimOpacity => NavigationRegion.BackDimOpacity;

    // PUSH (forward)

    /// <summary>
    /// The page arriving slides in from the right over the current page, which moves a quarter of
    /// the way aside and dims, as a navigation controller pushes.
    /// </summary>
    public virtual Task AnimatePushAsync(SpineTransitionContext transition)
    {
        transition.Front.TranslationX = transition.Width;

        return Task.WhenAll(
            transition.Front.SpineTranslateToAsync(0, 0, Duration, Ease),
            transition.Back.SpineTranslateToAsync(-transition.Width * Parallax, 0, Duration, Ease),
            transition.Dim.SpineFadeToAsync(DimOpacity, Duration, Ease));
    }

    /// <inheritdoc/>
    [Obsolete("Spine calls AnimatePushAsync, which moves whole layers; override that instead.")]
    public virtual async Task AnimateNavigateToShowAsync(View view)
    {
        var width = GetWidth(view);

        view.TranslationX = width;
        view.Opacity = 1;
        view.IsVisible = true;

        await view.TranslateToAsync(0, 0, Duration, Ease);
    }

    /// <inheritdoc/>
    [Obsolete("Spine calls AnimatePushAsync, which moves whole layers; override that instead.")]
    public virtual Task AnimateNavigateToHideAsync(View view)
    {
        view.IsVisible = false;
        return Task.CompletedTask;
    }

    // POP (back)

    /// <summary>
    /// The current page slides out to the right, uncovering the previous page as it moves back in
    /// from the side and its dim lifts: the same motion as a completed back-swipe.
    /// </summary>
    public virtual Task AnimatePopAsync(SpineTransitionContext transition)
    {
        transition.Back.TranslationX = -transition.Width * Parallax;
        transition.Dim.Opacity = DimOpacity;

        return Task.WhenAll(
            transition.Front.SpineTranslateToAsync(transition.Width, 0, Duration, Ease),
            transition.Back.SpineTranslateToAsync(0, 0, Duration, Ease),
            transition.Dim.SpineFadeToAsync(0, Duration, Ease));
    }

    /// <inheritdoc/>
    [Obsolete("Spine calls AnimatePopAsync, which moves whole layers; override that instead.")]
    public virtual async Task AnimateBackShowAsync(View view)
    {
        var width = GetWidth(view);

        view.TranslationX = -width * 0.25;
        view.Opacity = 1;
        view.IsVisible = true;

        await view.TranslateToAsync(0, 0, Duration, Ease);
    }

    /// <inheritdoc/>
    [Obsolete("Spine calls AnimatePopAsync, which moves whole layers; override that instead.")]
    public virtual Task AnimateBackHideAsync(View view)
    {
        view.IsVisible = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task ResetHiddenViewAsync(View view)
    {
        view.Opacity = 1;
        view.IsVisible = true;
        return Task.CompletedTask;
    }

    // Interactive back-swipe terminal animations

    /// <inheritdoc/>
    public virtual Task AnimateInteractiveBackCompleteAsync(View front, View back, double progress) =>
        Task.WhenAll(
            front.SpineTranslateToAsync(GetWidth(front), 0, Duration, Ease),
            back.SpineTranslateToAsync(0, 0, Duration, Ease));

    /// <inheritdoc/>
    public virtual Task AnimateInteractiveBackCancelAsync(View front, View back) =>
        Task.WhenAll(
            front.SpineTranslateToAsync(0, 0, Duration, Ease),
            back.SpineTranslateToAsync(-GetWidth(front) * Parallax, 0, Duration, Ease));

    /// <inheritdoc/>
    public virtual uint InteractiveGestureDuration => Duration;

    /// <inheritdoc/>
    public virtual Easing InteractiveGestureEasing => Ease;
}
