using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public sealed class SpanCompatibleDisplaysViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) :
    DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.SpanCompatibleDisplays, (config, value) => config.SpanCompatibleDisplays = value),
    IOverviewViewModel;
