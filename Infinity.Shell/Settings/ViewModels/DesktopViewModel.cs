using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed partial class DesktopViewModel :
    ObservableCollectionViewModel<IDesktopViewModel>,
    ISettingViewModel
{
    public DesktopViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        ITextLocalizer localizer,
        IEnumerable<IDesktopViewModel> items) :
        base(provider, factory, messenger, disposer, items) => Title = localizer.GetText("DesktopSectionTitle/Text");

    public string Glyph => "\uE80F";

    public string Title { get; }
}
