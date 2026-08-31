using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public abstract class DesktopSettingsSectionViewModel<TItem>(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, string title, IEnumerable<TItem> items) :
    ObservableCollectionViewModel<TItem>(provider, factory, messenger, disposer, items),
    IDesktopSettingsSectionViewModel
    where TItem : IDisposable
{
    public string Title { get; } = title;
}
