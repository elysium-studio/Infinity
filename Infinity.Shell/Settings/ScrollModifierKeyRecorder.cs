using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class ScrollModifierKeyRecorder(IHotKeysBuilder builder, HotKeysBuilderOptions builderOptions, IKeyLabelProvider labelProvider, ITextLocalizer localizer)
{
    private static readonly ScrollModifierKeyRecordingState IdleState = new(false, false, false, string.Empty, []);
    private bool isActive;

    public event Action<ScrollModifierKeyRecordingState>? StateChanged;

    public ScrollModifierKeyRecordingState State { get; private set; } = IdleState;

    private int RequiredKeyCount => Math.Max(2, builderOptions.KeyCount);

    public void Activate(List<List<int>>? combinations)
    {
        if (!isActive)
        {
            builder.Changed += HandleBuilderChanged;
            builder.Unavailable += HandleBuilderUnavailable;
            isActive = true;
        }

        Show(combinations);
    }


    public void Deactivate()
    {
        if (isActive)
        {
            builder.Changed -= HandleBuilderChanged;
            builder.Unavailable -= HandleBuilderUnavailable;
            isActive = false;
        }

        builder.Stop();
        Publish(IdleState);
    }


    public void Start()
    {
        Publish(new ScrollModifierKeyRecordingState(true, false, true, localizer.GetText("ScrollShortcutStartWithModifier"), []));
        if (!builder.Start())
        {
            Publish(new ScrollModifierKeyRecordingState(false, false, true, localizer.GetText("ScrollShortcutRecorderUnavailable"), []));
        }
    }


    public void Cancel()
    {
        builder.Stop();
        Publish(IdleState);
    }


    public bool TrySave(out List<List<int>> combinations)
    {
        HotKeysBuilderSnapshot snapshot = builder.Current;
        if (!State.IsRecording || !builder.IsComplete || snapshot.Keys.Count != RequiredKeyCount)
        {
            combinations = [];
            Publish(State with { CanSave = false, IsValidationOpen = true, ValidationMessage = localizer.GetText("ScrollShortcutPressKeysToSave", RequiredKeyCount) });
            return false;
        }

        combinations = [..snapshot.Combinations.Select(combination => combination.ToList())];
        builder.Stop();
        Show(combinations);
        return true;
    }


    public void Show(List<List<int>>? combinations)
    {
        if (combinations is null or { Count: 0 })
        {
            Publish(IdleState);
            return;
        }

        HashSet<string> seen = [];
        List<ScrollModifierKeyLabel> labels = [];
        foreach (List<int> combination in combinations)
        {
            foreach (int keyCode in combination)
            {
                ScrollModifierKeyLabel label = BuildLabel(keyCode);
                if (seen.Add(label.ToolTip))
                {
                    labels.Add(label);
                    break;
                }
            }
        }

        Publish(IdleState with { Labels = labels });
    }


    private void HandleBuilderChanged(object? sender, HotKeysBuilderSnapshot snapshot)
    {
        if (!State.IsRecording)
        {
            return;
        }

        List<ScrollModifierKeyLabel> labels = [..snapshot.Keys.Select(BuildLabel)];
        bool canSave = builder.IsComplete && snapshot.Keys.Count == RequiredKeyCount;
        bool isValidationOpen = true;
        string validationMessage;
        if (snapshot.Keys.Count == 0)
        {
            validationMessage = localizer.GetText("ScrollShortcutStartWithModifier");
        }
        else if (snapshot.Keys.Count < RequiredKeyCount)
        {
            validationMessage = localizer.GetText("ScrollShortcutPressMoreKeys", RequiredKeyCount - snapshot.Keys.Count);
        }
        else if (!builder.IsComplete)
        {
            validationMessage = localizer.GetText("ScrollShortcutUnavailable");
        }
        else
        {
            isValidationOpen = false;
            validationMessage = string.Empty;
        }

        Publish(new ScrollModifierKeyRecordingState(true, canSave, isValidationOpen, validationMessage, labels));
    }


    private void HandleBuilderUnavailable(object? sender, EventArgs args)
    {
        if (State.IsRecording)
        {
            Publish(State with { CanSave = false, IsValidationOpen = true, ValidationMessage = localizer.GetText("ScrollShortcutUnavailable") });
        }
    }


    private ScrollModifierKeyLabel BuildLabel(HotKey key)
    {
        string text = labelProvider.Shorten(key.Text);
        return new(text, key.Text);
    }


    private ScrollModifierKeyLabel BuildLabel(int keyCode)
    {
        string fullText = labelProvider.GetFullLabel(keyCode);
        string text = labelProvider.Shorten(fullText);
        return new(text, fullText);
    }


    private void Publish(ScrollModifierKeyRecordingState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
