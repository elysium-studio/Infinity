namespace Infinity.Application.Abstractions;

public interface IWindowFilterState
{
    bool IsActive { get; }

    bool FilterSelectionResolved { get; }

    string Filter { get; set; }

    string LastActivatedFilterText { get; }

    IntPtr LastActivatedHandle { get; }

    bool IsMatch(string title);

    void Apply(IEnumerable<ITrackedWindow> windows);

    void RecordActivation(string filterText, IntPtr handle);

    void ClearActivation();

    void ResetSelectionResolved();

    void MarkSelectionResolved();
}