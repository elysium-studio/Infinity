using System;

namespace Infinity.Platform.Abstractions;

public interface IWindowPreview :
    IDisposable
{
    nint WindowHandle { get; }

    nint Visual { get; }

    void Update(double width, double height, bool isVisible);
}
