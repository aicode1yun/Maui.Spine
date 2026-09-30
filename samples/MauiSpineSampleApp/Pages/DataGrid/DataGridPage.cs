namespace MauiSpineSampleApp.Pages.DataGrid;

// The grid scrolls inside its own list, which takes no scroll inset: keep the page clear of the
// home indicator, and the bar solid, since the example above the grid does not scroll under it.
[NavigableRegion(Title = "DataGrid", SafeAreaEdges = SafeAreaEdges.All, HeaderBarBackground = HeaderBarBackground.Solid)]
public partial class DataGridPage { public DataGridPage() => InitializeComponent(); }
