using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public partial class WindowJumpTriggerViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IKeyLabelProvider labelProvider) : ObservableViewModel(provider, factory, messenger, disposer),
    ITourViewModel,
    IRecipient<OptionsChangedEventArgs<Settings>>
{
    [ObservableProperty]
    private string firstModifier = string.Empty;

    [ObservableProperty]
    private string secondModifier = string.Empty;

    public bool CanGoBack => true;

    public bool CanGoNext => true;

    protected override void OnActivated()
    {
        ApplyModifiers(settings.ScrollModifierKeys);
        base.OnActivated();
    }

    public void Receive(OptionsChangedEventArgs<Settings> message) => ApplyModifiers(message.Options.ScrollModifierKeys);

    private void ApplyModifiers(List<List<int>>? combinations)
    {
        if (combinations is null or { Count: 0 })
        {
            return;
        }

        string first = labelProvider.GetShortLabel(combinations[0][0]);
        string second = combinations.Count > 1 ? labelProvider.GetShortLabel(combinations[1][0]) : first;

        dispatcher.Dispatch(() =>
        {
            FirstModifier = first;
            SecondModifier = second;
        });
    }
}