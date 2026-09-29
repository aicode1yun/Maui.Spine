using MauiBottomSheetPoc;

namespace MauiSpineSampleApp.Pages.Shortcuts;

public partial class ShortcutsPageViewModel : SampleViewModel
{
    private readonly ShortcutHandler _handler;

    public ShortcutsPageViewModel(ShortcutHandler handler)
    {
        _handler = handler;

        WhileVisible(() => { ShowLast(); _handler.Invoked += ShowLast; }, () => _handler.Invoked -= ShowLast);
    }

    public string Where => DeviceInfo.Platform == DevicePlatform.Android ? "Touch and hold the app icon on the home screen."
        : DeviceInfo.Platform == DevicePlatform.iOS ? "Touch and hold the app icon on the Home Screen."
        : DeviceInfo.Platform == DevicePlatform.WinUI ? "Right-click the app in the taskbar, or its icon in the notification area."
        : DeviceInfo.Platform == DevicePlatform.MacCatalyst ? "Click the app's icon in the menu bar."
        : "This platform shows no shortcuts.";

    [ObservableProperty]
    public partial string LastUsed { get; set; } = "";

    /// <summary>Runs a shortcut the way the platform does, through the same handler.</summary>
    [RelayCommand]
    private Task Run(string id) => _handler.InvokeAsync(id);

    private void ShowLast() => LastUsed = _handler.Last is var (shortcut, at)
        ? $"Last used: {shortcut.Title} at {at:HH:mm:ss}"
        : "No shortcut used since the app started.";
}
