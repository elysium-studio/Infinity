namespace Infinity.Platform.Abstractions;

public interface IWindowZOrder
{
    event EventHandler<IntPtr>? FocusedWindowChanged;

    event EventHandler? ZOrderChanged;

    void Start();

    void Stop();

    void BringToFront(IntPtr windowHandle);

    void NotifyFocusChanged(IntPtr windowHandle);

    void Refresh();
}