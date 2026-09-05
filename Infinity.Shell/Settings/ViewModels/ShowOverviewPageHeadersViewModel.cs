using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;

namespace Infinity.Shell;

public sealed class ShowOverviewPageHeadersViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) : DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.ShowOverviewPageHeaders, (config, value) => config.ShowOverviewPageHeaders = value), IOverviewViewModel;
