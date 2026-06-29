using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowFilterState(ITrackedWindowFilter filter) :
    IWindowFilterState
{
    public bool IsActive => filter.IsActive;

    public bool FilterSelectionResolved { get; private set; }

    public string LastActivatedFilterText { get; private set; } = string.Empty;

    public IntPtr LastActivatedHandle { get; private set; }

    public string Filter
    {
        get => filter.Filter;
        set => filter.Filter = value;
    }

    public bool IsMatch(string title) => filter.IsMatch(title);

    public void Apply(IReadOnlyCollection<ITrackedWindow> windows)
    {
        foreach (ITrackedWindow window in windows)
        {
            window.IsFiltered = !filter.IsMatch(window.Title);
        }
    }

    public void RecordActivation(string filterText, IntPtr handle)
    {
        LastActivatedFilterText = filterText;
        LastActivatedHandle = handle;
    }

    public void ClearActivation()
    {
        LastActivatedFilterText = string.Empty;
        LastActivatedHandle = default;
    }

    public void ResetSelectionResolved() => FilterSelectionResolved = false;

    public void MarkSelectionResolved() => FilterSelectionResolved = true;
}