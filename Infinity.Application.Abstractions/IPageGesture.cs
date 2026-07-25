namespace Infinity.Application.Abstractions;

public interface IPageGesture
{
    IReadOnlyCollection<int> TriggerKeys { get; }

    IReadOnlyCollection<int> RequiredKeys { get; }

    void Invoke(int virtualKeyCode);
}

public sealed class PointerScrollDeltaReceivedEventArgs(int delta) :
    EventArgs
{
    public int Delta { get; } = delta;
}

public sealed class PointerMiddleButtonClickedEventArgs :
    EventArgs;

public sealed class WorkspaceLayoutChangedEventArgs :
    EventArgs;

public sealed class WindowDragStartedEventArgs :
    EventArgs;

public sealed class WindowDragStoppedEventArgs :
    EventArgs;

public sealed class ScrollerScrollStartedEventArgs :
    EventArgs;

public sealed class TrackedWindowAddedEventArgs(TrackedWindow trackedWindow) :
    EventArgs
{
    public TrackedWindow TrackedWindow { get; } = trackedWindow;
}

public sealed class TrackedWindowRemovedEventArgs(IntPtr handle) :
    EventArgs
{
    public IntPtr Handle { get; } = handle;
}

public sealed class TrackedWindowChangedEventArgs(TrackedWindow trackedWindow) :
    EventArgs
{
    public TrackedWindow TrackedWindow { get; } = trackedWindow;
}

public sealed class WindowStackRefreshedEventArgs :
    EventArgs;

public sealed class WindowCollectionWorkspaceLayoutChangedEventArgs :
    EventArgs;

public sealed class WindowCollectionRefreshRequestedEventArgs :
    EventArgs;

public sealed class WindowDragMovedEventArgs :
    EventArgs;

public sealed class WindowDragScrolledEventArgs :
    EventArgs;

public sealed class DesktopBackgroundChangedEventArgs :
    EventArgs;