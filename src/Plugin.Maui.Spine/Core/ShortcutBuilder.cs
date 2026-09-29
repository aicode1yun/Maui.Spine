using System.Collections.Generic;

namespace Plugin.Maui.Spine.Core;

internal sealed class ShortcutBuilder : IShortcutBuilder
{
    private readonly List<SpineShortcut> _shortcuts = [];

    public IReadOnlyList<SpineShortcut> Shortcuts => _shortcuts;

    public IShortcutBuilder Add(string id, string title, bool showInTray = true, string? icon = null, string? subtitle = null)
    {
        _shortcuts.Add(new SpineShortcut(id, title, showInTray, icon, subtitle));
        return this;
    }
}
