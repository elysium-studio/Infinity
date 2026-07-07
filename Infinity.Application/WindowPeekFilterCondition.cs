using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowPeekFilterCondition :
    IWindowFilterCondition
{
    public IntPtr Handle { get; set; }

    public bool IsActive => Handle != default;

    public bool ShouldFilter(TrackedWindow trackedWindow) => trackedWindow.Handle != Handle;
}