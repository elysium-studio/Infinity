using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public abstract class DesktopOverviewBooleanSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer, Func<Settings, bool> read, Action<Settings, bool> write) :
    ObservableReadWriteViewModel<Settings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write);

public sealed class SpanCompatibleDisplaysViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) :
    DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.SpanCompatibleDisplays, (config, value) => config.SpanCompatibleDisplays = value),
    IOverviewViewModel;

public sealed class OverviewEdgeScrollingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) :
    DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.EnableOverviewEdgeScrolling, (config, value) => config.EnableOverviewEdgeScrolling = value),
    IScrollingViewModel;

public sealed class SnapAssistanceViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) :
    DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.EnableSnapAssistance, (config, value) => config.EnableSnapAssistance = value),
    IOverviewViewModel;
