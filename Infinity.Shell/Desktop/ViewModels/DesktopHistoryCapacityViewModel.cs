using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public partial class DesktopHistoryCapacityViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    Func<Settings, double> read,
    Action<Settings, double> write) :
    ObservableReadWriteViewModel<Settings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IDesktopViewModel
{
    [ObservableProperty]
    private bool isEnabled;

    public override void Activated()
    {
        base.Activated();
        IsEnabled = Options.DesktopHistoryEnabled;
    }

    protected override void OptionsChanged(Settings options) => IsEnabled = options.DesktopHistoryEnabled;
}
