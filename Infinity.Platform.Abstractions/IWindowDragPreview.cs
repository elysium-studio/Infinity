using System;

namespace Infinity.Platform.Abstractions;

public interface IWindowDragPreview :
    IDisposable
{
    void Move(WindowPreviewBounds bounds);
}
