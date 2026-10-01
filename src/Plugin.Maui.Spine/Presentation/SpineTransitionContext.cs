namespace Plugin.Maui.Spine.Presentation;

/// <summary>
/// The layers a push or a pop moves, passed to <see cref="ISpineTransitions.AnimatePushAsync"/> and
/// <see cref="ISpineTransitions.AnimatePopAsync"/>. Each layer reaches the edges of the screen, under
/// the system bars, so moving it moves the whole page. All of them are at rest when the transition
/// starts (no translation, the dim at opacity 0, apart from the incoming layer, which the transition
/// places itself) and are put back at rest after it, so a transition need not restore them.
/// </summary>
public sealed class SpineTransitionContext
{
    private readonly Action _flatten;

    internal SpineTransitionContext(View front, View back, View dim, double width, View incomingPage, View outgoingPage, Action flatten)
    {
        Front = front;
        Back = back;
        Dim = dim;
        Width = width;
        IncomingPage = incomingPage;
        OutgoingPage = outgoingPage;
        _flatten = flatten;
    }

    /// <summary>
    /// The upper layer: the page arriving on a push, the page leaving on a pop. It hides what is
    /// under it: it carries its page's background, or in a sheet the page underneath is cut off at
    /// its leading edge (the sheet's surface cannot move with a page). On iOS 26 it has the
    /// screen's or the sheet's rounded corners.
    /// </summary>
    public View Front { get; }

    /// <summary>The lower layer: the page being covered on a push, the page coming back on a pop.</summary>
    public View Back { get; }

    /// <summary>A black layer between <see cref="Back"/> and <see cref="Front"/>, at opacity 0 at rest.</summary>
    public View Dim { get; }

    /// <summary>The width of the region, the distance a page travels to leave it.</summary>
    public double Width { get; }

    /// <summary>The page arriving, inside its layer; what the per-page methods animate.</summary>
    internal View IncomingPage { get; }

    /// <summary>The page leaving, inside its layer.</summary>
    internal View OutgoingPage { get; }

    /// <summary>
    /// Makes <see cref="Front"/> transparent and square again, for the per-page methods: they hide
    /// the leaving page inside its layer, and an opaque front layer would then cover the page
    /// arriving under it.
    /// </summary>
    internal void Flatten() => _flatten();
}
