namespace Plugin.Maui.Spine.Core;

/// <summary>
/// Lets a sheet choose its detents for one navigation instead of taking them from its
/// <see cref="NavigableSheetAttribute"/>. Implement it on the sheet's view model; Spine asks after the view model
/// has received its navigation parameter, so the caller can pass the choice in with the parameter.
/// </summary>
/// <example>
/// <code><![CDATA[
/// public partial class PickerSheetViewModel : ViewModelBase, IReceivesNavigationParameter<PickerOptions>, ISheetDetentsProvider
/// {
///     public IReadOnlyList<string>? AllowedDetents { get; private set; }
///     public string? InitialDetent => null;
///
///     public Task OnNavigationParameterAsync(PickerOptions options)
///     {
///         AllowedDetents = options.Compact ? [SheetDetent.Medium] : [SheetDetent.FullScreen];
///         return Task.CompletedTask;
///     }
/// }
/// ]]></code>
/// </example>
public interface ISheetDetentsProvider
{
    /// <summary>
    /// The detents the user can snap the sheet to, in the format of <see cref="NavigableSheetAttribute.AllowedDetents"/>,
    /// or <see langword="null"/> to keep the attribute's.
    /// </summary>
    IReadOnlyList<string>? AllowedDetents { get; }

    /// <summary>
    /// The detent the sheet opens at, in the format of <see cref="NavigableSheetAttribute.InitialDetent"/>,
    /// or <see langword="null"/> for the first allowed detent.
    /// </summary>
    string? InitialDetent { get; }
}
