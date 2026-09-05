using System;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;
using NavigationStartedEventArgs = Infinity.Application.Abstractions.NavigationStartedEventArgs;

namespace Infinity.Shell.WinUI;

public sealed class NavigationModule : IModule
{
    public void Register(IServiceCollection services) => services.AddSingleton<IViewModelFactory>(provider => new ViewModelFactory((key, viewModelArgs) =>
    {
        key = key.EndsWith("ViewModel", StringComparison.Ordinal) ? key[..^"ViewModel".Length] : key;
        Type type = provider.GetRequiredKeyedService<ViewDescriptor>(key).ViewModelType!;
        if (viewModelArgs is { Length: > 0 })
        {
            IServiceFactory serviceFactory = provider.GetRequiredService<IServiceFactory>();
            return serviceFactory.Create(type, viewModelArgs);
        }

        return provider.GetRequiredKeyedService(type, key)!;
    })).AddSingleton<IViewFactory>(provider => new ViewFactory((key, viewArgs) =>
{
    key = key.EndsWith("ViewModel", StringComparison.Ordinal) ? key[..^"ViewModel".Length] : key;
    ViewDescriptor? descriptor = provider.GetKeyedService<ViewDescriptor>(key);
    Type? type = descriptor?.ViewType;
    if (type is null)
    {
        return null;
    }

    if (viewArgs is { Length: > 0 })
    {
        IServiceFactory serviceFactory = provider.GetRequiredService<IServiceFactory>();
        return serviceFactory.Create(type, viewArgs);
    }

    return provider.GetKeyedService(type, key);
})).AddServiceFactory().AddSingleton<WindowRegistry>().AddKeyedSingleton<INavigationHandler, WindowHandler>(typeof(Window)).AddKeyedSingleton<INavigationHandler, ContentDialogHandler>(typeof(ContentDialog)).AddKeyedSingleton<INavigationHandler, PopupHandler>(typeof(Popup)).Subscribe<IWindowNavigationCoordinator>((provider, coordinator) =>
{
    IMessenger messenger = provider.GetRequiredService<IMessenger>();
    void HandleNavigationStarted(object? sender, NavigationStartedEventArgs args) => messenger.Send(args);
    void HandleNavigationCompleted(object? sender, EventArgs args) => messenger.Send(new NavigationCompletedEventArgs());
    void HandleWindowActivationRequested(object? sender, EventArgs args) => messenger.Send(new WindowActivationRequestedEventArgs());
    coordinator.NavigationStarted += HandleNavigationStarted;
    coordinator.NavigationCompleted += HandleNavigationCompleted;
    coordinator.WindowActivationRequested += HandleWindowActivationRequested;
    return () =>
    {
        coordinator.NavigationStarted -= HandleNavigationStarted;
        coordinator.NavigationCompleted -= HandleNavigationCompleted;
        coordinator.WindowActivationRequested -= HandleWindowActivationRequested;
    };
});
}
