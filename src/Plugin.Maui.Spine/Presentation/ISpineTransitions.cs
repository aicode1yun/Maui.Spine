namespace Plugin.Maui.Spine.Presentation;

/// <summary>
/// Defines the animated transitions used during navigation within a <see cref="NavigationRegion"/>.
/// The default implementation is <see cref="DefaultSpineTransitions"/>.
/// Implement this interface and register it in DI to supply custom animations.
/// Platform-specific implementations can be registered conditionally in <c>MauiProgram.cs</c>.
/// </summary>
public interface ISpineTransitions
{
    /// <summary>Animates the initial root page when it is first placed into the navigation region.</summary>
    /// <param name="view">The root page appearing for the first time.</param>
    Task AnimateSetRootAsync(View view);

    /// <summary>Animates the incoming page during a forward navigation.</summary>
    /// <param name="view">The page sliding/fading in.</param>
    Task AnimateNavigateToShowAsync(View view);

    /// <summary>Animates the outgoing page during a forward navigation.</summary>
    /// <param name="view">The page sliding/fading out.</param>
    Task AnimateNavigateToHideAsync(View view);

    /// <summary>Animates the incoming (previous) page during a back navigation.</summary>
    /// <param name="view">The page sliding/fading back in.</param>
    Task AnimateBackShowAsync(View view);

    /// <summary>Animates the outgoing (current) page during a back navigation.</summary>
    /// <param name="view">The page sliding/fading out.</param>
    Task AnimateBackHideAsync(View view);

    /// <summary>
    /// Animates a forward navigation: <see cref="SpineTransitionContext.Front"/> holds the page
    /// arriving, <see cref="SpineTransitionContext.Back"/> the page it covers. The default calls
    /// <see cref="AnimateNavigateToShowAsync"/> and <see cref="AnimateNavigateToHideAsync"/> with
    /// the two pages, so an implementation written for those keeps working.
    /// </summary>
    /// <param name="transition">The layers to move.</param>
    Task AnimatePushAsync(SpineTransitionContext transition)
    {
        transition.Flatten();
        return Task.WhenAll(AnimateNavigateToShowAsync(transition.IncomingPage), AnimateNavigateToHideAsync(transition.OutgoingPage));
    }

    /// <summary>
    /// Animates a back navigation: <see cref="SpineTransitionContext.Front"/> holds the page
    /// leaving, <see cref="SpineTransitionContext.Back"/> the page coming back. The default calls
    /// <see cref="AnimateBackShowAsync"/> and <see cref="AnimateBackHideAsync"/> with the two pages.
    /// </summary>
    /// <param name="transition">The layers to move.</param>
    Task AnimatePopAsync(SpineTransitionContext transition)
    {
        transition.Flatten();
        return Task.WhenAll(AnimateBackShowAsync(transition.IncomingPage), AnimateBackHideAsync(transition.OutgoingPage));
    }

    /// <summary>
    /// Restores a page to its fully visible resting state after it has been hidden by
    /// <see cref="AnimateNavigateToHideAsync"/> and placed back on the navigation stack.
    /// Called after a forward navigation completes, so the page is ready to animate back in
    /// when the user eventually returns to it.
    /// The default implementation resets <c>Opacity</c> to <c>1</c> and <c>IsVisible</c> to <c>true</c>.
    /// </summary>
    /// <param name="view">The page that was hidden and now sits on the back stack.</param>
    Task ResetHiddenViewAsync(View view)
    {
        view.Opacity = 1;
        view.IsVisible = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the user completes an interactive back-swipe gesture to finish the pop animation.
    /// </summary>
    /// <param name="front">The foreground (current) page.</param>
    /// <param name="back">The background (previous) page.</param>
    /// <param name="progress">The drag offset in device-independent units reached when the gesture ended.</param>
    Task AnimateInteractiveBackCompleteAsync(View front, View back, double progress);

    /// <summary>
    /// Called when the user cancels an interactive back-swipe gesture to animate the page back to
    /// its original position.
    /// </summary>
    /// <param name="front">The foreground (current) page.</param>
    /// <param name="back">The background (previous) page.</param>
    Task AnimateInteractiveBackCancelAsync(View front, View back);

    /// <summary>
    /// Duration in milliseconds of the dim's fade when an interactive back-swipe is released
    /// (completed or cancelled). Defaults to <c>250</c>.
    /// </summary>
    uint InteractiveGestureDuration => 250;

    /// <summary>
    /// Easing of the dim's fade when an interactive back-swipe is released.
    /// Defaults to <see cref="Easing.CubicOut"/>.
    /// </summary>
    Easing InteractiveGestureEasing => Easing.CubicOut;
}
