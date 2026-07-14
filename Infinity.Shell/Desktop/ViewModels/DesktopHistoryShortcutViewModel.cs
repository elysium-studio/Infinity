using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public partial class DesktopHistoryShortcutViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    Func<Settings, List<List<int>>?> read,
    Action<Settings, List<List<int>>?> write,
    IHotKeysBuilder builder,
    IKeyLabelProvider labelProvider,
    ITextLocalizer localizer,
    DesktopHistoryShortcutKind kind) :
    ObservableReadWriteViewModel<Settings, List<List<int>>>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IDesktopViewModel
{
    private const int RequiredKeyCount = 3;
    private List<List<int>> previousValue = [];

    [ObservableProperty]
    private bool isRecording;

    [ObservableProperty]
    private bool canSave;

    [ObservableProperty]
    private bool isValidationOpen;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    private List<ModifierKeyViewModel> labels = [];

    [ObservableProperty]
    private bool isEnabled;

    public string Header => localizer.GetText(kind == DesktopHistoryShortcutKind.Back
        ? "DesktopHistoryBackShortcutHeader"
        : "DesktopHistoryForwardShortcutHeader");

    public string Description => localizer.GetText(kind == DesktopHistoryShortcutKind.Back
        ? "DesktopHistoryBackShortcutDescription"
        : "DesktopHistoryForwardShortcutDescription");

    public override void Activated()
    {
        base.Activated();
        builder.Changed -= HandleBuilderChanged;
        builder.Unavailable -= HandleBuilderUnavailable;
        builder.Changed += HandleBuilderChanged;
        builder.Unavailable += HandleBuilderUnavailable;
        IsEnabled = Options.DesktopHistoryEnabled;
        BuildLabels(Value);
    }

    public override void Deactivated()
    {
        builder.Changed -= HandleBuilderChanged;
        builder.Unavailable -= HandleBuilderUnavailable;
        builder.Stop();

        Dispatcher.Dispatch(() =>
        {
            IsRecording = false;
            CanSave = false;
            IsValidationOpen = false;
            ValidationMessage = string.Empty;
        });

        base.Deactivated();
    }

    public override void Dispose()
    {
        builder.Changed -= HandleBuilderChanged;
        builder.Unavailable -= HandleBuilderUnavailable;
        builder.Dispose();
        base.Dispose();
    }

    public void StartRecording()
    {
        previousValue = Value?.Select(group => group.ToList()).ToList() ?? [];

        Dispatcher.Dispatch(() =>
        {
            IsRecording = true;
            CanSave = false;
            Labels = [];
            IsValidationOpen = true;
            ValidationMessage = localizer.GetText("DesktopHistoryShortcutStartWithModifiers");
        });

        if (!builder.Start())
        {
            Dispatcher.Dispatch(() =>
            {
                IsRecording = false;
                IsValidationOpen = true;
                ValidationMessage = localizer.GetText("ScrollShortcutRecorderUnavailable");
            });
        }
    }

    public void CancelRecording()
    {
        builder.Stop();

        Dispatcher.Dispatch(() =>
        {
            IsRecording = false;
            CanSave = false;
            IsValidationOpen = false;
            ValidationMessage = string.Empty;
        });

        Value = previousValue;
        BuildLabels(previousValue);
    }

    public void SaveRecording()
    {
        HotKeysBuilderSnapshot snapshot = builder.Current;

        if (!IsRecording || !builder.IsComplete || !DesktopHistoryShortcutValidator.IsValid(snapshot))
        {
            ShowValidation("DesktopHistoryShortcutInvalid");
            return;
        }

        List<List<int>> shortcut = snapshot.Combinations.Select(group => group.ToList()).ToList();
        List<List<int>> otherShortcut = kind == DesktopHistoryShortcutKind.Back
            ? Options.DesktopHistoryForwardShortcut
            : Options.DesktopHistoryBackShortcut;

        if (DesktopHistoryShortcutValidator.AreEquivalent(shortcut, otherShortcut) ||
            DesktopHistoryShortcutValidator.ConflictsWithPageNavigation(shortcut, Options.ScrollModifierKeys))
        {
            ShowValidation("DesktopHistoryShortcutConflict");
            return;
        }

        builder.Stop();

        Dispatcher.Dispatch(() =>
        {
            IsRecording = false;
            CanSave = false;
            IsValidationOpen = false;
            ValidationMessage = string.Empty;
        });

        Value = shortcut;
        BuildLabels(shortcut);
    }

    protected override void OptionsChanged(Settings options)
    {
        IsEnabled = options.DesktopHistoryEnabled;
        BuildLabels(kind == DesktopHistoryShortcutKind.Back
            ? options.DesktopHistoryBackShortcut
            : options.DesktopHistoryForwardShortcut);
    }

    private void HandleBuilderChanged(object? sender, HotKeysBuilderSnapshot snapshot)
    {
        List<ModifierKeyViewModel> newLabels = snapshot.Keys.Select(BuildLabel).ToList();

        Dispatcher.Dispatch(() =>
        {
            if (!IsRecording)
            {
                return;
            }

            Labels = newLabels;
            CanSave = builder.IsComplete && DesktopHistoryShortcutValidator.IsValid(snapshot);

            if (snapshot.Keys.Count == 0)
            {
                IsValidationOpen = true;
                ValidationMessage = localizer.GetText("DesktopHistoryShortcutStartWithModifiers");
            }
            else if (snapshot.Keys.Count < RequiredKeyCount)
            {
                IsValidationOpen = true;
                ValidationMessage = localizer.GetText("ScrollShortcutPressMoreKeys", RequiredKeyCount - snapshot.Keys.Count);
            }
            else if (!DesktopHistoryShortcutValidator.IsValid(snapshot))
            {
                IsValidationOpen = true;
                ValidationMessage = localizer.GetText("DesktopHistoryShortcutInvalid");
            }
            else
            {
                IsValidationOpen = false;
                ValidationMessage = string.Empty;
            }
        });
    }

    private void HandleBuilderUnavailable(object? sender, EventArgs args) => ShowValidation("ScrollShortcutUnavailable");

    private void ShowValidation(string resourceKey) => Dispatcher.Dispatch(() =>
    {
        CanSave = false;
        IsValidationOpen = true;
        ValidationMessage = localizer.GetText(resourceKey);
    });

    private void BuildLabels(IEnumerable<IEnumerable<int>>? combinations)
    {
        if (combinations is null)
        {
            Dispatcher.Dispatch(() => Labels = []);
            return;
        }

        List<ModifierKeyViewModel> newLabels = combinations
            .Select(group => group.FirstOrDefault())
            .Where(key => key != 0)
            .Select(BuildLabel)
            .ToList();

        Dispatcher.Dispatch(() => Labels = newLabels);
    }

    private ModifierKeyViewModel BuildLabel(HotKey key) =>
        new(labelProvider.Shorten(key.Text), ToolTip: key.Text);

    private ModifierKeyViewModel BuildLabel(int keyCode)
    {
        string fullText = labelProvider.GetFullLabel(keyCode);
        return new ModifierKeyViewModel(labelProvider.Shorten(fullText), ToolTip: fullText);
    }
}
