using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public sealed class SnapAssistanceViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) :
    DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.EnableSnapAssistance, (config, value) => config.EnableSnapAssistance = value),
    IOverviewViewModel;
