using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class PagesViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, ITextLocalizer localizer, IEnumerable<IPagesViewModel> items) :
    DesktopSettingsSectionViewModel<IPagesViewModel>(provider, factory, messenger, disposer, localizer.GetText("PagesSettingsSectionTitle"), items);
