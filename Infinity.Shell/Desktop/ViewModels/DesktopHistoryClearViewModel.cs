using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public partial class DesktopHistoryClearViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    IDesktopNavigationHistory history) :
    ObservableViewModel(provider, factory, messenger, disposer),
    IDesktopViewModel
{
    [ObservableProperty]
    private bool canClear;

    public override void Activated()
    {
        history.Changed -= HandleHistoryChanged;
        history.Changed += HandleHistoryChanged;
        Refresh();
    }

    public override void Deactivated() => history.Changed -= HandleHistoryChanged;

    public override void Dispose()
    {
        history.Changed -= HandleHistoryChanged;
        base.Dispose();
    }

    public void Clear() => history.Clear();

    private void HandleHistoryChanged(object? sender, EventArgs args) => dispatcher.Dispatch(Refresh);

    private void Refresh() => CanClear = history.BackEntries.Count > 0 || history.ForwardEntries.Count > 0;
}
