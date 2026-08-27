using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class ScrollingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IScrollingViewModel> items) :
    DesktopSettingsSectionViewModel<IScrollingViewModel>(provider, factory, messenger, disposer, localizer.GetText("ScrollingSettingsSectionTitle"), items);
