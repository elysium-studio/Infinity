using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class VirtualPagesCountViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    Func<Settings, double> read,
    Action<Settings, double> write) : ObservableReadWriteViewModel<Settings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IDesktopViewModel
{
    [ObservableProperty]
    private bool isEnabled;

    public override void Activated()
    {
        base.Activated();
        Dispatcher.Dispatch(() => IsEnabled = Options.VirtualPagesMode == VirtualPagesMode.Fixed);
    }

    protected override void OptionsChanged(Settings options)
    {
        base.OptionsChanged(options);
        Dispatcher.Dispatch(() => IsEnabled = options.VirtualPagesMode == VirtualPagesMode.Fixed);
    }
}