namespace MauiSpineSampleApp.Pages;

public partial class OptionsSheetViewModel(INavigationService _navigation) : ViewModelBase, IReceivesNavigationParameter<SampleOptions>
{
    [ObservableProperty]
    public partial IReadOnlyList<ObservableObject> Options { get; set; } = [];

    public Task OnNavigationParameterAsync(SampleOptions options)
    {
        Title = options.Title;
        Options = options.Options;
        return Task.CompletedTask;
    }

    [PageAction("Done")]
    [RelayCommand]
    private async Task Done() => await _navigation.CloseAsync();
}
