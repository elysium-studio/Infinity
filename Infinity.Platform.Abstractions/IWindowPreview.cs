using System;

namespace Infinity.Platform.Abstractions;

public interface IWindowPreview :
    IDisposable
{
    nint WindowHandle { get; }

    void SetPlacement(double x,
        double y,
        double width,
        double height,
        int zIndex,
        bool isVisible,
        bool isElevated);
}
