using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;

namespace Infinity.Shell;

public sealed partial class PreviewViewModel :
    ObservableCollectionViewModel<IPreviewViewModel>,
    ISettingViewModel
{
    public PreviewViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        ITextLocalizer localizer,
        IEnumerable<IPreviewViewModel> items) :
        base(provider, factory, messenger, disposer, items) => Title = localizer.GetText("PreviewSectionTitle/Text");

    public string Glyph => "\uE890";

    public string Title { get; }
}
