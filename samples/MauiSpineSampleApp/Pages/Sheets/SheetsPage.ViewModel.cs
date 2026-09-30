namespace MauiSpineSampleApp.Pages.Sheets;

public partial class SheetsPageViewModel(INavigationService _navigation) : SampleViewModel
{
    [RelayCommand] private async Task ShowDetents() => await _navigation.NavigateToAsync<SamplePage>();
    [RelayCommand] private async Task ShowCompact() => await _navigation.NavigateToAsync<SmallSheetPage>();
    [RelayCommand] private async Task ShowFullScreen() => await _navigation.NavigateToAsync<FullscreenSheetPage>();
    [RelayCommand] private async Task ShowBlurred() => await _navigation.NavigateToAsync<SimpleBottomSheetPage>();
    [RelayCommand] private async Task ShowEditSheet() => await _navigation.NavigateToAsync<EditSheetPage>();
    [RelayCommand] private async Task ShowLoginSheet() => await _navigation.NavigateToAsync<LoginSheetPage>();
    [RelayCommand] private async Task ShowLateAction() => await _navigation.NavigateToAsync<LateActionSheet>();
}
