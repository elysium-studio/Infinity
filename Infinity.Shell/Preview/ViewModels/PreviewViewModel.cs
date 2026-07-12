using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class PreviewViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IEnumerable<IPreviewViewModel> items) : ObservableCollectionViewModel<IPreviewViewModel>(provider, factory, messenger, disposer, items),
    ISettingViewModel;