using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed partial class DesktopShortcutHintsViewModel : ObservableObject, IRecipient<OptionsChangedEventArgs<Settings>>
{
    private const int DefaultFirstModifier = 0x5B;
    private const int DefaultSecondModifier = 0xA2;
    private readonly IDispatcher dispatcher;
    private readonly IKeyLabelProvider labelProvider;
    [ObservableProperty]
    private string firstModifier = string.Empty;
    [ObservableProperty]
    private string secondModifier = string.Empty;

    public DesktopShortcutHintsViewModel(IMessenger messenger, IDispatcher dispatcher, Settings settings, IKeyLabelProvider labelProvider)
    {
        this.dispatcher = dispatcher;
        this.labelProvider = labelProvider;
        ApplyModifiers(settings.ScrollModifierKeys);
        messenger.Register(this);
    }


    public void Receive(OptionsChangedEventArgs<Settings> message) => ApplyModifiers(message.Options.ScrollModifierKeys);

    private void ApplyModifiers(List<List<int>>? combinations)
    {
        List<int> keys = combinations?.Where(combination => combination.Count > 0).Select(combination => combination[0]).Take(2).ToList() ?? [];
        int firstKey = keys.Count > 0 ? keys[0] : DefaultFirstModifier;
        int secondKey = keys.Count > 1 ? keys[1] : DefaultSecondModifier;
        string first = labelProvider.GetShortLabel(firstKey);
        string second = labelProvider.GetShortLabel(secondKey);
        dispatcher.Dispatch(() =>
        {
            FirstModifier = first;
            SecondModifier = second;
        });
    }
}
