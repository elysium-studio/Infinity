using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;

namespace Infinity.Shell;

public sealed class ShowOverviewKeyboardShortcutButtonViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer) : DesktopOverviewBooleanSettingViewModel(provider, factory, messenger, disposer, dispatcher, settings, writer, config => config.ShowOverviewKeyboardShortcutButton, (config, value) => config.ShowOverviewKeyboardShortcutButton = value), IOverviewViewModel;
