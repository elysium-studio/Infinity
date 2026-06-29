using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public partial class ScrollModifierKeyViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    Func<Settings, List<List<int>>?> read,
    Action<Settings, List<List<int>>?> write,
    IHotKeysBuilder builder,
    HotKeysBuilderOptions builderOptions,
    IKeyLabelProvider labelProvider) :
    ObservableReadWriteViewModel<Settings, List<List<int>>>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IDesktopViewModel
{
    private List<List<int>> previousValue = [];
    private List<List<int>> pendingCombinations = [];

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

    private int RequiredKeyCount => Math.Max(2, builderOptions.KeyCount);

    public override void Activated()
    {
        base.Activated();

        builder.Changed += HandleBuilderChanged;
        builder.Unavailable += HandleBuilderUnavailable;

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

    public void StartRecording()
    {
        previousValue = Value ?? [];
        pendingCombinations = [];

        Dispatcher.Dispatch(() =>
        {
            IsRecording = true;
            CanSave = false;
            Labels = [];
            IsValidationOpen = true;
            ValidationMessage = "Start with Ctrl, Alt, Shift or Win.";
        });

        if (!builder.Start())
        {
            Dispatcher.Dispatch(() =>
            {
                IsRecording = false;
                CanSave = false;
                IsValidationOpen = true;
                ValidationMessage = "The keyboard recorder could not be started.";
            });
        }
    }

    public void CancelRecording()
    {
        builder.Stop();

        pendingCombinations = [];

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

        if (!IsRecording || !builder.IsComplete || snapshot.Keys.Count != RequiredKeyCount)
        {
            Dispatcher.Dispatch(() =>
            {
                CanSave = false;
                IsValidationOpen = true;
                ValidationMessage = $"Press {RequiredKeyCount} keys to save this shortcut.";
            });

            return;
        }

        pendingCombinations = snapshot.Combinations.Select(combination => combination.ToList()).ToList();

        builder.Stop();

        Dispatcher.Dispatch(() =>
        {
            IsRecording = false;
            CanSave = false;
            IsValidationOpen = false;
            ValidationMessage = string.Empty;
        });

        Value = pendingCombinations;
        BuildLabels(Value);

        pendingCombinations = [];
    }

    private void HandleBuilderChanged(object? sender, HotKeysBuilderSnapshot snapshot) =>
        ApplySnapshot(snapshot);

    private void HandleBuilderUnavailable(object? sender, EventArgs args)
    {
        Dispatcher.Dispatch(() =>
        {
            if (!IsRecording)
            {
                return;
            }

            CanSave = false;
            IsValidationOpen = true;
            ValidationMessage = "That shortcut is already used by Windows or another app.";
        });
    }

    private void ApplySnapshot(HotKeysBuilderSnapshot snapshot)
    {
        List<List<int>> combinations = snapshot.Combinations.Select(combination => combination.ToList()).ToList();
        List<ModifierKeyViewModel> newLabels = snapshot.Keys.Select(BuildLabel).ToList();

        Dispatcher.Dispatch(() =>
        {
            if (!IsRecording)
            {
                return;
            }

            pendingCombinations = combinations;
            Labels = newLabels;
            CanSave = builder.IsComplete && snapshot.Keys.Count == RequiredKeyCount;

            if (snapshot.Keys.Count == 0)
            {
                IsValidationOpen = true;
                ValidationMessage = "Start with Ctrl, Alt, Shift or Win.";
                return;
            }

            if (snapshot.Keys.Count < RequiredKeyCount)
            {
                IsValidationOpen = true;
                ValidationMessage = $"Press {RequiredKeyCount - snapshot.Keys.Count} more key.";
                return;
            }

            if (!builder.IsComplete)
            {
                IsValidationOpen = true;
                ValidationMessage = "That shortcut is already used by Windows or another app.";
                return;
            }

            IsValidationOpen = false;
            ValidationMessage = string.Empty;
        });
    }

    private void BuildLabels(List<List<int>>? combinations)
    {
        if (combinations is null or { Count: 0 })
        {
            Dispatcher.Dispatch(() => Labels = []);
            return;
        }

        HashSet<string> seen = [];
        List<ModifierKeyViewModel> newLabels = [];

        foreach (List<int> combination in combinations)
        {
            foreach (int keyCode in combination)
            {
                ModifierKeyViewModel label = BuildLabel(keyCode);

                if (seen.Add(label.ToolTip ?? label.Text))
                {
                    newLabels.Add(label);
                    break;
                }
            }
        }

        Dispatcher.Dispatch(() => Labels = newLabels);
    }

    private ModifierKeyViewModel BuildLabel(HotKey key)
    {
        string text = labelProvider.Shorten(key.Text);

        return new ModifierKeyViewModel(text, ToolTip: key.Text);
    }

    private ModifierKeyViewModel BuildLabel(int keyCode)
    {
        string fullText = labelProvider.GetFullLabel(keyCode);
        string text = labelProvider.Shorten(fullText);

        return new ModifierKeyViewModel(text, ToolTip: fullText);
    }
}