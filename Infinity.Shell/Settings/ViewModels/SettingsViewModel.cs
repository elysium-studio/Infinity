using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public sealed partial class SettingsViewModel :
    ObservableCollectionViewModel<ISettingViewModel>
{
    [ObservableProperty]
    private ISettingViewModel? currentView;

    public SettingsViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IEnumerable<ISettingViewModel> items) :
        base(provider, factory, messenger, disposer, items) => CurrentView = SelectedItem;

    public void NavigateTo(ISettingViewModel? viewModel) => CurrentView = viewModel;
}