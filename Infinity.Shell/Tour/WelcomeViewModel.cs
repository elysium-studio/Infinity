using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;

namespace Infinity.Shell;

public class WelcomeViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer) : ObservableViewModel(provider, factory, messenger, disposer),
    ITourViewModel
{
    public bool CanGoBack => false;

    public bool CanGoNext => true;
}

