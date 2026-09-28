#if IOS || MACCATALYST

using System.Runtime.CompilerServices;
using UIKit;

namespace Plugin.Maui.Spine.Presentation;

/// <summary>
/// Shows and hides Liquid Glass buttons the way UIKit does. A glass view cannot be drawn
/// translucent: fading it through its alpha shows a flat grey stand-in until it is opaque again.
/// So the glass materializes and dissolves (its effect animates in and out) and only the content on
/// it, the title and the image, fades.
/// </summary>
internal static class GlassAppearance
{
    // The effect a glass view had when it was first seen, to put back after a dissolve.
    static readonly ConditionalWeakTable<UIVisualEffectView, UIVisualEffect> Effects = new();

    /// <summary>Whether <paramref name="view"/> holds glass this helper can animate.</summary>
    public static bool Applies(View view) =>
        OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26)
            ? view.Handler?.PlatformView is UIView root && GlassViews(root).Any()
            : false;

    /// <summary>Materializes (<paramref name="show"/>) or dissolves the glass in <paramref name="view"/> and fades its content.</summary>
    public static Task AnimateAsync(View view, bool show, uint duration, double delay = 0)
    {
        if (view.Handler?.PlatformView is not UIView root)
            return Task.CompletedTask;

        var glass = GlassViews(root).ToList();
        var content = Content(root).ToList();

        foreach (var effectView in glass)
        {
            if (effectView.Effect is { } effect)
                Effects.AddOrUpdate(effectView, effect);
        }

        if (show)
        {
            UIView.PerformWithoutAnimation(() =>
            {
                foreach (var effectView in glass)
                    effectView.Effect = null;
                foreach (var item in content)
                    item.Alpha = 0;
            });
        }

        var done = new TaskCompletionSource();
        UIView.AnimateNotify(
            duration / 1000.0, delay,
            UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.CurveEaseOut | UIViewAnimationOptions.AllowUserInteraction,
            () =>
            {
                foreach (var effectView in glass)
                    effectView.Effect = show && Effects.TryGetValue(effectView, out var effect) ? effect : null;
                foreach (var item in content)
                    item.Alpha = show ? 1 : 0;
            },
            _ => done.TrySetResult());

        return done.Task;
    }

    /// <summary>Fades only the title and image of the glass buttons in <paramref name="view"/>.</summary>
    public static Task FadeContentAsync(View view, bool show, uint duration, double delay = 0)
    {
        if (view.Handler?.PlatformView is not UIView root)
            return Task.CompletedTask;

        var content = Content(root).ToList();
        var done = new TaskCompletionSource();
        UIView.AnimateNotify(
            duration / 1000.0, delay,
            UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.CurveEaseOut | UIViewAnimationOptions.AllowUserInteraction,
            () =>
            {
                foreach (var item in content)
                    item.Alpha = show ? 1 : 0;
            },
            _ => done.TrySetResult());

        return done.Task;
    }

    /// <summary>Sets the content's alpha at once, outside any animation.</summary>
    public static void SetContentAlpha(View view, nfloat alpha)
    {
        if (view.Handler?.PlatformView is not UIView root)
            return;

        UIView.PerformWithoutAnimation(() =>
        {
            foreach (var item in Content(root))
                item.Alpha = alpha;
        });
    }

    static IEnumerable<UIVisualEffectView> GlassViews(UIView root) =>
        Descendants(root).OfType<UIVisualEffectView>();

    // A button's title and image; the glass view's own subviews are its material, not content.
    static IEnumerable<UIView> Content(UIView root) =>
        Descendants(root).Where(v => v is UILabel or UIImageView && !IsInsideGlass(v));

    static bool IsInsideGlass(UIView view)
    {
        for (var parent = view.Superview; parent is not null; parent = parent.Superview)
        {
            if (parent is UIVisualEffectView)
                return true;
        }

        return false;
    }

    static IEnumerable<UIView> Descendants(UIView root)
    {
        foreach (var child in root.Subviews)
        {
            yield return child;

            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}

#endif
