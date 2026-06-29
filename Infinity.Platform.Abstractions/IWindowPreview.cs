using System;

namespace Infinity.Platform.Abstractions;

public interface IWindowPreview :
    IDisposable
{
    nint WindowHandle { get; }

    object? KeepAlive { get; set; }

    event Action? PreviewInvalidated;

    void SetTarget(nint sharedTargetHandle, double width, double height, bool isVisible);

    void SetPlacement(double x, double y, double width, double height, bool isVisible);
}