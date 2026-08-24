namespace Infinity.Platform.Abstractions;

public interface IWindowPreview :
    IDisposable
{
    nint WindowHandle { get; }

    void SetTarget(nint sharedTargetHandle, double width, double height, bool isVisible);
}