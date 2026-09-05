using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class ScrollTriggerViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IKeyLabelProvider labelProvider) : TourShortcutViewModel(provider, factory, messenger, disposer, dispatcher, settings, labelProvider)
{
}
