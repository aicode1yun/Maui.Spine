namespace Plugin.Maui.Spine.Controls;

/// <summary>How an <see cref="AnimatedLabel"/> moves its text.</summary>
public enum AnimatedLabelMode
{
    /// <summary>Text that does not fit scrolls side to side; a new text fades in.</summary>
    Marquee,

    /// <summary>The characters that change roll vertically, like an odometer; the text does not scroll.</summary>
    RollingNumber,
}
