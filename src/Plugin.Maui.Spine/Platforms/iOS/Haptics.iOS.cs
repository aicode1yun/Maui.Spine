using UIKit;

namespace Plugin.Maui.Spine.Extensions;

public static partial class Haptics
{
    static UINotificationFeedbackGenerator? _notification;
    static UISelectionFeedbackGenerator? _selection;
    static readonly Dictionary<UIImpactFeedbackStyle, UIImpactFeedbackGenerator> _impacts = [];

    static partial void PlayPlatform(Haptic haptic)
    {
        switch (haptic)
        {
            case Haptic.Success or Haptic.Warning or Haptic.Error:
                var notification = _notification ??= new UINotificationFeedbackGenerator();
                notification.NotificationOccurred(haptic switch
                {
                    Haptic.Success => UINotificationFeedbackType.Success,
                    Haptic.Warning => UINotificationFeedbackType.Warning,
                    _ => UINotificationFeedbackType.Error,
                });
                break;
            case Haptic.Selection:
                (_selection ??= new UISelectionFeedbackGenerator()).SelectionChanged();
                break;
            default:
                ImpactGenerator(haptic).ImpactOccurred();
                break;
        }
    }

    static partial void PreparePlatform(Haptic haptic)
    {
        switch (haptic)
        {
            case Haptic.Success or Haptic.Warning or Haptic.Error:
                (_notification ??= new UINotificationFeedbackGenerator()).Prepare();
                break;
            case Haptic.Selection:
                (_selection ??= new UISelectionFeedbackGenerator()).Prepare();
                break;
            default:
                ImpactGenerator(haptic).Prepare();
                break;
        }
    }

    static UIImpactFeedbackGenerator ImpactGenerator(Haptic haptic)
    {
        var style = haptic switch
        {
            Haptic.Medium => UIImpactFeedbackStyle.Medium,
            Haptic.Heavy => UIImpactFeedbackStyle.Heavy,
            Haptic.Soft => UIImpactFeedbackStyle.Soft,
            Haptic.Rigid => UIImpactFeedbackStyle.Rigid,
            _ => UIImpactFeedbackStyle.Light,
        };

        // The view-bound initializer of iOS 17.5 only places feedback on a trackpad; the phone plays either.
#pragma warning disable CA1422
        if (!_impacts.TryGetValue(style, out var generator))
            _impacts[style] = generator = new UIImpactFeedbackGenerator(style);
#pragma warning restore CA1422

        return generator;
    }
}
