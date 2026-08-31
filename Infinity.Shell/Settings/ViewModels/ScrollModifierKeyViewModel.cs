using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed partial class ScrollModifierKeyViewModel(
    IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    Settings settings,
    IWritableOptions<Settings> writer,
    Func<Settings, List<List<int>>?> read,
    Action<Settings, List<List<int>>?> write,
    ScrollModifierKeyRecorder recorder) :
    ObservableReadWriteViewModel<Settings, List<List<int>>>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IScrollingViewModel
{
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

    public override void Activated()
    {
        base.Activated();
        recorder.StateChanged += HandleRecorderStateChanged;
        recorder.Activate(Value);
    }

    public override void Deactivated()
    {
        recorder.StateChanged -= HandleRecorderStateChanged;
        recorder.Deactivate();
        base.Deactivated();
    }

    public void StartRecording()
    {
        previousValue = Value ?? [];
        recorder.Start();
    }

    public void CancelRecording()
    {
        recorder.Cancel();
        Value = previousValue;
        recorder.Show(previousValue);
    }

    public void SaveRecording()
    {
        if (recorder.TrySave(out List<List<int>> combinations))
        {
            Value = combinations;
        }
    }

    private void HandleRecorderStateChanged(ScrollModifierKeyRecordingState state) =>
        Dispatcher.Dispatch(() => ApplyState(state));

    private void ApplyState(ScrollModifierKeyRecordingState state)
    {
        IsRecording = state.IsRecording;
        CanSave = state.CanSave;
        IsValidationOpen = state.IsValidationOpen;
        ValidationMessage = state.ValidationMessage;
        Labels = [.. state.Labels.Select(label => new ModifierKeyViewModel(label.Text, ToolTip: label.ToolTip))];
    }
}
