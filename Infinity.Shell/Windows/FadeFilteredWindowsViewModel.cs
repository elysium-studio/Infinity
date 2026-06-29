using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class FadeFilteredWindowsViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    Func<Settings, bool> read,
    Action<Settings, bool> write) : ObservableReadWriteViewModel<Settings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IDesktopViewModel;