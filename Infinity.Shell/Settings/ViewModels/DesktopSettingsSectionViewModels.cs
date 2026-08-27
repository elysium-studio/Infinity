using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using System.Collections;

namespace Infinity.Shell;

public interface IDesktopSettingsSectionViewModel :
    IDesktopViewModel,
    IEnumerable
{
    string Title { get; }
}

public abstract class DesktopSettingsSectionViewModel<TItem>(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, string title, IEnumerable<TItem> items) :
    ObservableCollectionViewModel<TItem>(provider, factory, messenger, disposer, items),
    IDesktopSettingsSectionViewModel
    where TItem : IDisposable
{
    public string Title { get; } = title;
}

public sealed class PagesViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IPagesViewModel> items) :
    DesktopSettingsSectionViewModel<IPagesViewModel>(provider, factory, messenger, disposer, localizer.GetText("PagesSettingsSectionTitle"), items);

public sealed class ScrollingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IScrollingViewModel> items) :
    DesktopSettingsSectionViewModel<IScrollingViewModel>(provider, factory, messenger, disposer, localizer.GetText("ScrollingSettingsSectionTitle"), items);

public sealed class OverviewViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IOverviewViewModel> items) :
    DesktopSettingsSectionViewModel<IOverviewViewModel>(provider, factory, messenger, disposer, localizer.GetText("OverviewSettingsSectionTitle"), items);

public sealed class AdvancedViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IAdvancedViewModel> items) :
    DesktopSettingsSectionViewModel<IAdvancedViewModel>(provider, factory, messenger, disposer, localizer.GetText("AdvancedSettingsSectionTitle"), items);
