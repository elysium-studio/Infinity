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

public class TrackedWindowAddedEventArgs(TrackedWindow trackedWindow) :
    EventArgs
{
    public TrackedWindow TrackedWindow { get; } = trackedWindow;
}

public class TrackedWindowRemovedEventArgs(IntPtr handle) :
    EventArgs
{
    public IntPtr Handle { get; } = handle;
}

public class TrackedWindowChangedEventArgs(TrackedWindow trackedWindow) :
    EventArgs
{
    public TrackedWindow TrackedWindow { get; } = trackedWindow;
}

public class WindowStackRefreshedEventArgs :
    EventArgs;

public class WindowCollectionWorkspaceLayoutChangedEventArgs :
    EventArgs;

public class WindowCollectionRefreshRequestedEventArgs :
    EventArgs;

public class WindowDragMovedEventArgs :
    EventArgs;

public class WindowDragScrolledEventArgs :
    EventArgs;

public class DesktopBackgroundChangedEventArgs :
    EventArgs;