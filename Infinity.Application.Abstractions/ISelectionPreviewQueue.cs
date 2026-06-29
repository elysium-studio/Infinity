namespace Infinity.Application.Abstractions;

public interface ISelectionPreviewQueue
{
    void Queue(IntPtr handle, Func<IntPtr> factory);

    void Cancel();
}
