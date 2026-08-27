using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class AdvancedViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IAdvancedViewModel> items) :
    DesktopSettingsSectionViewModel<IAdvancedViewModel>(provider, factory, messenger, disposer, localizer.GetText("AdvancedSettingsSectionTitle"), items);
