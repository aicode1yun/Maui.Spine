namespace Plugin.Maui.Spine.Controls;

/// <summary>How the header image of a <see cref="HeroCollectionView"/> behaves as the header collapses.</summary>
public enum HeroImageCollapse
{
    /// <summary>The image stays centred in the part of the header that is still showing.</summary>
    Center,

    /// <summary>The image slides up with the header, leaving its bottom edge showing.</summary>
    Slide
}
