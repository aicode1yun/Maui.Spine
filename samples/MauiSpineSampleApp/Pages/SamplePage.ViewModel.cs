namespace MauiSpineSampleApp.Pages;

public partial class SamplePageViewModel(INavigationService _navigation) : ViewModelBase
{
    [ObservableProperty]
    public partial string? UserName { get; set; }

    [RelayCommand]
    private async Task Next() => await _navigation.NavigateToAsync<SamplePage2>();

    [PageAction("Save", Role = PageActionRole.Confirm)]
    [RelayCommand]
    private Task DummyAction() => Task.CompletedTask;
}
