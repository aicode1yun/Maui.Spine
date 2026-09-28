namespace Plugin.Maui.Spine.Controls;

/// <summary>Where a <see cref="SpineRow"/> shows its <see cref="SpineRow.Value"/>.</summary>
public enum SpineRowValuePlacement
{
    /// <summary>The platform's place: under the title on Android (a preference's summary), at the end of the row elsewhere.</summary>
    Auto,

    /// <summary>At the end of the row, before the accessory and the chevron.</summary>
    Trailing,

    /// <summary>Under the title, and under the detail when the row has one.</summary>
    Below,
}
