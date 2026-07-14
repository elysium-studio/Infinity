using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public class DesktopHistoryForwardShortcutViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    IHotKeysBuilder builder,
    IKeyLabelProvider labelProvider,
    ITextLocalizer localizer) :
    DesktopHistoryShortcutViewModel(provider,
        factory,
        messenger,
        disposer,
        dispatcher,
        settings,
        writer,
        options => options.DesktopHistoryForwardShortcut,
        (options, shortcut) => options.DesktopHistoryForwardShortcut = shortcut!,
        builder,
        labelProvider,
        localizer,
        DesktopHistoryShortcutKind.Forward);
