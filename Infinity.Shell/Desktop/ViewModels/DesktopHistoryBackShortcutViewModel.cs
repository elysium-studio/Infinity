using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public class DesktopHistoryBackShortcutViewModel(IServiceProvider provider,
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
        options => options.DesktopHistoryBackShortcut,
        (options, shortcut) => options.DesktopHistoryBackShortcut = shortcut!,
        builder,
        labelProvider,
        localizer,
        DesktopHistoryShortcutKind.Back);
