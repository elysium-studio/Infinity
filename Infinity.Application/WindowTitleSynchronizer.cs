using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class WindowTitleSynchronizer(IWindowStore repository,
    IWindowTitleReader titleReader,
    IWindowEventListener listener) :
    IWindowTitleSynchronizer
{
    public void Start()
    {
        listener.WindowTitleChanged += HandleWindowTitleChanged;
        repository.WindowAdded += HandleWindowAdded;
    }

    public void Stop()
    {
        listener.WindowTitleChanged -= HandleWindowTitleChanged;
        repository.WindowAdded -= HandleWindowAdded;
    }

    private void HandleWindowAdded(object? sender, TrackedWindow trackedWindow)
    {
        trackedWindow.Title = titleReader.GetTitle(trackedWindow.Handle);

        if (!string.IsNullOrEmpty(trackedWindow.Title))
        {
            repository.NotifyChanged(trackedWindow.Handle);
        }
    }

    private void HandleWindowTitleChanged(IntPtr windowHandle)
    {
        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            return;
        }

        trackedWindow.Title = titleReader.GetTitle(windowHandle);
        repository.NotifyChanged(windowHandle);
    }
}