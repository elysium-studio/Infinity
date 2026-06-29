using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class WindowsViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IEnumerable<IWindowsViewModel> items) : ObservableCollectionViewModel<IWindowsViewModel>(provider, factory, messenger, disposer, items),
    ISettingViewModel;