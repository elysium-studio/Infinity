using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class WindowTitleSynchronizer(IWindowStore repository,
    IWindowTitleReader titleReader,
    IWindowEventListener listener) :
    IWindowTitleSynchronizer
{
    private bool isStarted;

    public void Start()
    {
        if (isStarted)
        {
            return;
        }

        isStarted = true;

        listener.WindowTitleChanged += HandleWindowTitleChanged;
        repository.WindowAdded += HandleWindowAdded;

        SynchronizeExistingWindows();
    }

    public void Stop()
    {
        if (!isStarted)
        {
            return;
        }

        isStarted = false;

        listener.WindowTitleChanged -= HandleWindowTitleChanged;
        repository.WindowAdded -= HandleWindowAdded;
    }

    private void SynchronizeExistingWindows()
    {
        foreach (TrackedWindow trackedWindow in repository)
        {
            SynchronizeTitle(trackedWindow.Handle);
        }
    }

    private void HandleWindowAdded(object? sender, TrackedWindow trackedWindow)
    {
        SynchronizeTitle(trackedWindow.Handle);
    }

    private void HandleWindowTitleChanged(IntPtr windowHandle)
    {
        SynchronizeTitle(windowHandle);
    }

    private void SynchronizeTitle(IntPtr windowHandle)
    {
        if (windowHandle == default)
        {
            return;
        }

        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            return;
        }

        string title = titleReader.GetTitle(windowHandle);

        if (string.Equals(trackedWindow.Title, title, StringComparison.Ordinal))
        {
            return;
        }

        trackedWindow.Title = title;
        repository.NotifyChanged(windowHandle);
    }
}