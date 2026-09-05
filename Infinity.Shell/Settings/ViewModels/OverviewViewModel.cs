using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class OverviewViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IOverviewViewModel> items) : DesktopSettingsSectionViewModel<IOverviewViewModel>(provider, factory, messenger, disposer, localizer.GetText("OverviewSettingsSectionTitle"), items);
