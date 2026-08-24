using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Infinity.Shell;

public sealed partial class WindowsViewModel :
    ObservableCollectionViewModel<IWindowsViewModel>,
    ISettingViewModel
{
    public WindowsViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        ITextLocalizer localizer,
        IEnumerable<IWindowsViewModel> items) :
        base(provider, factory, messenger, disposer, items) => Title = localizer.GetText("WindowsSectionTitle/Text");

    public string Glyph => "\uE737";

    public string Title { get; }
}