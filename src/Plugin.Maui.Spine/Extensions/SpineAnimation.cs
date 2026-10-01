namespace Plugin.Maui.Spine.Extensions;

/// <summary>
/// <see cref="ViewExtensions.TranslateToAsync"/> and <see cref="ViewExtensions.FadeToAsync"/> on the
/// platform's own animation engine where it has one. On iOS and Mac Catalyst the motion runs in Core
/// Animation, outside the app's main thread, so it keeps the display's frame rate while the page
/// arriving is still loading; elsewhere, and for an easing the platform cannot express, it runs on
/// MAUI's ticker as the MAUI methods do.
/// </summary>
public static partial class SpineAnimation
{
    /// <summary>Animates <see cref="VisualElement.TranslationX"/> and <see cref="VisualElement.TranslationY"/> to the given values.</summary>
    /// <returns>A task that completes when the animation has finished.</returns>
    public static Task SpineTranslateToAsync(this VisualElement view, double x, double y, uint length = 250, Easing? easing = null)
    {
        if (view.TranslationX == x && view.TranslationY == y)
            return Task.CompletedTask;

        return AnimateOnPlatform(view, length, easing, () =>
            {
                view.TranslationX = x;
                view.TranslationY = y;
            })
            ?? view.TranslateToAsync(x, y, length, easing);
    }

    /// <summary>Animates <see cref="VisualElement.Opacity"/> to <paramref name="opacity"/>.</summary>
    /// <returns>A task that completes when the animation has finished.</returns>
    public static Task SpineFadeToAsync(this VisualElement view, double opacity, uint length = 250, Easing? easing = null)
    {
        if (view.Opacity == opacity)
            return Task.CompletedTask;

        return AnimateOnPlatform(view, length, easing, () => view.Opacity = opacity)
            ?? view.FadeToAsync(opacity, length, easing);
    }

    /// <summary>
    /// Runs <paramref name="apply"/> inside a platform animation and returns its completion, or
    /// <see langword="null"/> where the platform cannot animate it.
    /// </summary>
    private static partial Task? AnimateOnPlatform(VisualElement view, uint length, Easing? easing, Action apply);

#if !IOS && !MACCATALYST
    private static partial Task? AnimateOnPlatform(VisualElement view, uint length, Easing? easing, Action apply) => null;
#endif
}
