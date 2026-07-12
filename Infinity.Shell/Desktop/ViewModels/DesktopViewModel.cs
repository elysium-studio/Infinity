using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class DesktopViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IEnumerable<IDesktopViewModel> items) : ObservableCollectionViewModel<IDesktopViewModel>(provider, factory, messenger, disposer, items),
    ISettingViewModel;