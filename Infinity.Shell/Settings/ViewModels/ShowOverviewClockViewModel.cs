using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;

namespace Infinity.Shell;

public sealed class ShowOverviewClockViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) : DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.ShowOverviewClock, (config, value) => config.ShowOverviewClock = value), IOverviewViewModel;
