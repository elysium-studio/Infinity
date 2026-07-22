using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class WindowPeekSource :
    IWindowPeekSource
{
    public nint Handle { get; set; }

    public bool IsActive => Handle != default;

    public bool RevealsWindow(TrackedWindow trackedWindow) => trackedWindow.Handle == Handle;
}
