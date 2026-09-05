using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed partial class ResetPageCustomizationsViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, Settings settings, IWritableOptions<Settings> writer, ITextLocalizer localizer) : ObservableViewModel(provider, factory, messenger, disposer), IAdvancedViewModel, IRecipient<OptionsChangedEventArgs<Settings>>
{
    private readonly IDispatcher dispatcher = dispatcher;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    private bool hasCustomizations;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    private bool isResetting;
    [ObservableProperty]
    private bool isErrorOpen;
    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool CanReset => HasCustomizations && !IsResetting;

    public string DialogTitle => localizer.GetText("ResetPageCustomizationsDialogTitle");

    public string DialogMessage => localizer.GetText("ResetPageCustomizationsDialogMessage");

    public string DialogPrimaryButtonText => localizer.GetText("ResetPageCustomizationsDialogPrimaryButton");

    public string DialogCloseButtonText => localizer.GetText("ResetPageCustomizationsDialogCloseButton");

    public override void Activated() => Apply(settings);

    protected override void RegisterMessages() => Messenger.Register<OptionsChangedEventArgs<Settings>>(this);

    public void Receive(OptionsChangedEventArgs<Settings> message) => dispatcher.Dispatch(() => Apply(message.Options));

    public async Task ResetAsync()
    {
        if (!CanReset)
        {
            return;
        }

        IsResetting = true;
        IsErrorOpen = false;
        try
        {
            Settings updated = await writer.ReadAsync() ?? new Settings();
            updated.PageLayouts = [];
            updated.PageTitles = [];
            await writer.WriteAsync(updated);
            Apply(updated);
        }
        catch
        {
            ErrorMessage = localizer.GetText("ResetPageCustomizationsError");
            IsErrorOpen = true;
        }
        finally
        {
            IsResetting = false;
        }
    }


    private void Apply(Settings options) => HasCustomizations = options.PageLayouts is { Count: > 0 } || options.PageTitles is { Count: > 0 };
}
