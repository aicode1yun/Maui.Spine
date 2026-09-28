using Plugin.Maui.Spine.Extensions;

namespace Plugin.Maui.Spine.Core;

/// <summary>
/// Haptics Spine plays by itself, exposed as <see cref="SpineOptions.Haptics"/>. Each is
/// <see cref="Haptic.None"/> by default; the platform's own tab bars and sheets play nothing.
/// </summary>
/// <example>
/// <code>
/// builder.UseSpine(options =>
/// {
///     options.Haptics.TabSwitch = Haptic.Selection;
///     options.Haptics.SheetDetent = Haptic.Light;
///     options.Haptics.DismissBlocked = Haptic.Warning;
/// });
/// </code>
/// </example>
public sealed class SpineHapticsOptions
{
    /// <summary>Played when the user switches tabs; not when the app switches with navigation.</summary>
    public Haptic TabSwitch { get; set; }

    /// <summary>Played when a sheet the user drags settles on another detent. iOS and Android.</summary>
    public Haptic SheetDetent { get; set; }

    /// <summary>
    /// Played when the user tries to close a sheet (swipe, backdrop, close or back button) and its
    /// <see cref="ViewModelBase.OnCloseRequestedAsync"/> refuses at once. A guard that awaits, for a
    /// "discard changes?" prompt, plays nothing: the prompt is the answer. iOS and Android.
    /// </summary>
    public Haptic DismissBlocked { get; set; }
}
