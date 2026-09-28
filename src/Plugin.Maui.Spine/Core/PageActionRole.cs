namespace Plugin.Maui.Spine.Core;

/// <summary>
/// What a header-bar action does, so Spine can draw it the platform's way: a sheet confirms with a checkmark and
/// cancels with an X, as the iOS 26 Human Interface Guidelines ask, and the same on Android.
/// </summary>
/// <remarks>
/// A role picks the icon and what a screen reader says (the action's text, or the localised
/// <c>Spine.Header.Done</c> / <c>Spine.Header.Cancel</c>). An explicit <see cref="PageAction.Svg"/> or
/// <see cref="PageAction.Description"/> still wins. Use a text action only for something with no standard icon.
/// </remarks>
public enum PageActionRole
{
    /// <summary>No role: the action shows its text or its own icon.</summary>
    None,

    /// <summary>Saves or accepts and closes, shown as a checkmark in the trailing slot.</summary>
    Confirm,

    /// <summary>Discards and closes, shown as an X in the leading slot unless a placement is given.</summary>
    Cancel,
}
