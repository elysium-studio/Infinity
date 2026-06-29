using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class PreviewSizeViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    Func<Settings, int> read,
    Action<Settings, int> write) : ObservableReadWriteViewModel<Settings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IPreviewViewModel;