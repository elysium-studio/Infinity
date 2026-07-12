using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class SettingsViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IEnumerable<ISettingViewModel> items) :
    ObservableCollectionViewModel<ISettingViewModel>(provider, factory, messenger, disposer, items);