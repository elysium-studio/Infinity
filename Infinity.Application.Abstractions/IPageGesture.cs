namespace Infinity.Application.Abstractions;

public interface IPageGesture
{
    IReadOnlyCollection<int> TriggerKeys { get; }

    IReadOnlyCollection<int> RequiredKeys { get; }

    void Invoke(int virtualKeyCode);
}


public class PointerScrollDeltaReceivedEventArgs(int delta) : 
    EventArgs
{
    public int Delta { get; } = delta;
}

public class PointerMiddleButtonClickedEventArgs :
    EventArgs;

public class WorkspaceLayoutChangedEventArgs : 
    EventArgs;

public class WindowDragStartedEventArgs : 
    EventArgs;

public class WindowDragStoppedEventArgs :
    EventArgs;

public class ScrollerScrollStartedEventArgs :
    EventArgs;