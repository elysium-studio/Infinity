namespace Infinity.Shell;

public sealed class DesktopWindowSelectionModel
{
    private readonly HashSet<nint> selectedHandles = [];

    public nint FocusedHandle { get; private set; }

    public IReadOnlySet<nint> SelectedHandles => selectedHandles;

    public void Focus(nint handle) => FocusedHandle = handle;

    public bool ToggleSelected(nint handle)
    {
        if (handle == 0)
        {
            return false;
        }

        if (selectedHandles.Remove(handle))
        {
            return false;
        }

        selectedHandles.Add(handle);
        return true;
    }

    public void RemoveSelected(nint handle) => selectedHandles.Remove(handle);

    public void ClearSelectedHandles() => selectedHandles.Clear();

    public void Clear()
    {
        selectedHandles.Clear();
        FocusedHandle = 0;
    }
}
