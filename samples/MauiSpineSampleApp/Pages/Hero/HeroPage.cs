namespace MauiSpineSampleApp.Pages.Hero;

// The header bar lies over the photo with nothing behind it, so the hero's compact header is what
// shows behind the back button and the title once the list has scrolled.
[NavigableRegion(Title = "HeroCollectionView", HeaderBar = HeaderBarMode.Overlay, HeaderBarBackground = HeaderBarBackground.Transparent,
    HeaderBarForeground = "#FFFFFF", HeaderBarGlass = HeaderBarGlass.Clear,
    StatusBarStyle = StatusBarStyle.LightContent, ScrollInset = SafeAreaEdges.Bottom)]
public partial class HeroPage { public HeroPage() => InitializeComponent(); }
